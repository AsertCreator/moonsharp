using MoonSharp.Interpreter.Execution;

namespace MoonSharp.Interpreter.Tree
{
	/// <summary>
	/// The Luau type syntax - see https://luau.org/typecheck
	///
	/// MoonSharp has no type checker, so nothing here builds a tree: each method validates that
	/// what follows is a well formed type and advances the lexer past it. Annotations therefore
	/// cost nothing at run time and cannot change what a script does.
	///
	/// The grammar accepted is
	///
	///		Type       := Intersect ('|' Intersect)*
	///		Intersect  := Suffixed ('&amp;' Suffixed)*
	///		Suffixed   := Simple '?'*
	///		Simple     := 'nil' | 'true' | 'false' | String        -- singletons
	///					| 'typeof' '(' expr ')'
	///					| '(' ParamList ')' '->' ReturnType        -- function type
	///					| '(' Type ')'
	///					| '{' TableBody '}'
	///					| Name ('.' Name)? GenericArgs?
	///
	/// </summary>
	static class TypeAnnotation
	{
		public static bool IsEnabled(ScriptLoadingContext lcontext)
		{
			return (lcontext.Script.Options.LuauFeatures & LuauFeatures.TypeAnnotations) != 0;
		}

		/// <summary>
		/// Whether the lexer is sitting on the '&lt;' of a generic parameter declaration. A '&lt;'
		/// can never be a comparison in the places this is asked, so no lookahead is needed.
		/// </summary>
		public static bool AtGenericParams(ScriptLoadingContext lcontext)
		{
			return IsEnabled(lcontext) && lcontext.Lexer.Current.Type == TokenType.Op_LessThan;
		}

		/// <summary>
		/// Skips a ': Type' if one is there and the feature is on. Used everywhere a name may
		/// carry an annotation - locals, parameters, loop variables.
		/// </summary>
		public static void SkipOptionalAnnotation(ScriptLoadingContext lcontext)
		{
			if (!IsEnabled(lcontext))
				return;

			if (lcontext.Lexer.Current.Type != TokenType.Colon)
				return;

			lcontext.Lexer.Next();
			SkipType(lcontext);
		}

		/// <summary>
		/// Skips a '&lt;T, U..., V = number&gt;' generic parameter declaration if one is there.
		/// </summary>
		public static void SkipOptionalGenericParams(ScriptLoadingContext lcontext)
		{
			if (!IsEnabled(lcontext))
				return;

			if (lcontext.Lexer.Current.Type != TokenType.Op_LessThan)
				return;

			lcontext.Lexer.Next();

			while (true)
			{
				Expect(lcontext, TokenType.Name);

				// a generic type pack, 'T...'
				if (lcontext.Lexer.Current.Type == TokenType.VarArgs)
					lcontext.Lexer.Next();

				// a default, 'T = number'
				if (lcontext.Lexer.Current.Type == TokenType.Op_Assignment)
				{
					lcontext.Lexer.Next();
					SkipTypeOrPack(lcontext);
				}

				if (lcontext.Lexer.Current.Type != TokenType.Comma)
					break;

				lcontext.Lexer.Next();
			}

			Expect(lcontext, TokenType.Op_GreaterThan);
		}

		/// <summary>
		/// Skips the ': R' return annotation of a function if one is there. A return annotation may
		/// be a parenthesised list, since a Lua function returns any number of values.
		/// </summary>
		public static void SkipOptionalReturnAnnotation(ScriptLoadingContext lcontext)
		{
			if (!IsEnabled(lcontext))
				return;

			if (lcontext.Lexer.Current.Type != TokenType.Colon)
				return;

			lcontext.Lexer.Next();
			SkipReturnType(lcontext);
		}

		private static void SkipReturnType(ScriptLoadingContext lcontext)
		{
			if (lcontext.Lexer.Current.Type == TokenType.Brk_Open_Round)
			{
				Token open = lcontext.Lexer.Current;
				lcontext.Lexer.Next();

				if (lcontext.Lexer.Current.Type != TokenType.Brk_Close_Round)
					SkipTypeOrPackList(lcontext);

				CheckMatch(lcontext, open, TokenType.Brk_Close_Round, ")");

				// '(A, B) -> C' as a return type means a function is being returned
				if (lcontext.Lexer.Current.Type == TokenType.Arrow)
				{
					lcontext.Lexer.Next();
					SkipReturnType(lcontext);
				}

				return;
			}

			SkipType(lcontext);
		}

		// ---------------------------------------------------------------
		// The type grammar proper
		// ---------------------------------------------------------------

		public static void SkipType(ScriptLoadingContext lcontext)
		{
			// a leading separator is allowed, so a long union may be written one alternative per
			// line with each line starting with the '|'
			if (lcontext.Lexer.Current.Type == TokenType.Lambda)
				lcontext.Lexer.Next();

			SkipIntersection(lcontext);

			while (lcontext.Lexer.Current.Type == TokenType.Lambda)
			{
				lcontext.Lexer.Next();
				SkipIntersection(lcontext);
			}
		}

		private static void SkipIntersection(ScriptLoadingContext lcontext)
		{
			if (lcontext.Lexer.Current.Type == TokenType.Op_Ampersand)
				lcontext.Lexer.Next();

			SkipSuffixed(lcontext);

			while (lcontext.Lexer.Current.Type == TokenType.Op_Ampersand)
			{
				lcontext.Lexer.Next();
				SkipSuffixed(lcontext);
			}
		}

		private static void SkipSuffixed(ScriptLoadingContext lcontext)
		{
			SkipSimple(lcontext);

			while (lcontext.Lexer.Current.Type == TokenType.Op_Question)
				lcontext.Lexer.Next();
		}

		private static void SkipSimple(ScriptLoadingContext lcontext)
		{
			Token t = lcontext.Lexer.Current;

			switch (t.Type)
			{
				case TokenType.Nil:
				case TokenType.True:
				case TokenType.False:
				case TokenType.String:
				case TokenType.String_Long:
					// singleton types - 'nil', 'true', '"up"'
					lcontext.Lexer.Next();
					return;
				case TokenType.Brk_Open_Round:
					SkipParenthesised(lcontext);
					return;
				case TokenType.Brk_Open_Curly:
					SkipTable(lcontext);
					return;
				case TokenType.Name:
					SkipNamed(lcontext);
					return;
				default:
					throw new SyntaxErrorException(t, "type expected near '{0}'", t.Text)
					{
						IsPrematureStreamTermination = (t.Type == TokenType.Eof)
					};
			}
		}

		/// <summary>
		/// 'Name', 'Mod.Name', 'Array&lt;T&gt;' and the special form 'typeof(expr)'.
		/// </summary>
		private static void SkipNamed(ScriptLoadingContext lcontext)
		{
			Token name = lcontext.Lexer.Current;
			lcontext.Lexer.Next();

			if (name.Text == "typeof" && lcontext.Lexer.Current.Type == TokenType.Brk_Open_Round)
			{
				// the operand is an ordinary expression, not a type, and it is discarded with
				// everything else here - it is never evaluated
				Token open = lcontext.Lexer.Current;
				lcontext.Lexer.Next();
				Expression.Expr(lcontext);
				CheckMatch(lcontext, open, TokenType.Brk_Close_Round, ")");
				return;
			}

			if (lcontext.Lexer.Current.Type == TokenType.Dot)
			{
				lcontext.Lexer.Next();
				Expect(lcontext, TokenType.Name);
			}

			SkipOptionalGenericArgs(lcontext);
		}

		private static void SkipOptionalGenericArgs(ScriptLoadingContext lcontext)
		{
			if (lcontext.Lexer.Current.Type != TokenType.Op_LessThan)
				return;

			lcontext.Lexer.Next();

			if (lcontext.Lexer.Current.Type != TokenType.Op_GreaterThan)
				SkipTypeOrPackList(lcontext);

			Expect(lcontext, TokenType.Op_GreaterThan);
		}

		/// <summary>
		/// A '(' in type position is either a parenthesised type, or the parameter list of a
		/// function type. Which one it is only becomes clear at the ')', so both are parsed the
		/// same way and the '->' afterwards decides.
		/// </summary>
		private static void SkipParenthesised(ScriptLoadingContext lcontext)
		{
			Token open = lcontext.Lexer.Current;
			lcontext.Lexer.Next();

			if (lcontext.Lexer.Current.Type != TokenType.Brk_Close_Round)
			{
				while (true)
				{
					SkipFunctionTypeParam(lcontext);

					if (lcontext.Lexer.Current.Type != TokenType.Comma)
						break;

					lcontext.Lexer.Next();
				}
			}

			CheckMatch(lcontext, open, TokenType.Brk_Close_Round, ")");

			if (lcontext.Lexer.Current.Type == TokenType.Arrow)
			{
				lcontext.Lexer.Next();
				SkipReturnType(lcontext);
			}
		}

		/// <summary>
		/// One entry of a function type's parameter list. Luau allows the parameters to be named
		/// for documentation, as in '(count: number) -&gt; ()'.
		/// </summary>
		private static void SkipFunctionTypeParam(ScriptLoadingContext lcontext)
		{
			if (lcontext.Lexer.Current.Type == TokenType.VarArgs)
			{
				lcontext.Lexer.Next();
				SkipType(lcontext);
				return;
			}

			if (lcontext.Lexer.Current.Type == TokenType.Name && lcontext.Lexer.PeekNext().Type == TokenType.Colon)
			{
				lcontext.Lexer.Next();
				lcontext.Lexer.Next();
			}

			SkipTypeOrPack(lcontext);
		}

		/// <summary>
		/// '{ number }', '{ x: number, y: number }', '{ [string]: number }', or any mix of the
		/// last two. The separator may be a ',' or a ';' and a trailing one is allowed.
		/// </summary>
		private static void SkipTable(ScriptLoadingContext lcontext)
		{
			Token open = lcontext.Lexer.Current;
			lcontext.Lexer.Next();

			if (lcontext.Lexer.Current.Type == TokenType.Brk_Close_Curly)
			{
				lcontext.Lexer.Next();
				return;
			}

			// '{ T }' is the array shorthand, and is the only form with no key
			if (!IsTableEntryStart(lcontext))
			{
				SkipType(lcontext);
				CheckMatch(lcontext, open, TokenType.Brk_Close_Curly, "}");
				return;
			}

			while (true)
			{
				if (lcontext.Lexer.Current.Type == TokenType.Brk_Open_Square)
				{
					// an indexer, '[K]: V'
					Token bracket = lcontext.Lexer.Current;
					lcontext.Lexer.Next();
					SkipType(lcontext);
					CheckMatch(lcontext, bracket, TokenType.Brk_Close_Square, "]");
				}
				else
				{
					Expect(lcontext, TokenType.Name);
				}

				Expect(lcontext, TokenType.Colon);
				SkipType(lcontext);

				if (lcontext.Lexer.Current.Type == TokenType.Comma || lcontext.Lexer.Current.Type == TokenType.SemiColon)
				{
					lcontext.Lexer.Next();

					// a trailing separator before the '}'
					if (lcontext.Lexer.Current.Type == TokenType.Brk_Close_Curly)
						break;

					continue;
				}

				break;
			}

			CheckMatch(lcontext, open, TokenType.Brk_Close_Curly, "}");
		}

		private static bool IsTableEntryStart(ScriptLoadingContext lcontext)
		{
			if (lcontext.Lexer.Current.Type == TokenType.Brk_Open_Square)
				return true;

			// 'x: number' is a property, whereas '{ Foo }' and '{ Foo.Bar }' are array shorthands
			return lcontext.Lexer.Current.Type == TokenType.Name
				&& lcontext.Lexer.PeekNext().Type == TokenType.Colon;
		}

		/// <summary>
		/// A type, or a type pack in the positions where one is allowed ('...T' and 'T...').
		/// </summary>
		private static void SkipTypeOrPack(ScriptLoadingContext lcontext)
		{
			if (lcontext.Lexer.Current.Type == TokenType.VarArgs)
			{
				lcontext.Lexer.Next();
				SkipType(lcontext);
				return;
			}

			// a generic pack reference, 'T...'
			if (lcontext.Lexer.Current.Type == TokenType.Name && lcontext.Lexer.PeekNext().Type == TokenType.VarArgs)
			{
				lcontext.Lexer.Next();
				lcontext.Lexer.Next();
				return;
			}

			SkipType(lcontext);
		}

		private static void SkipTypeOrPackList(ScriptLoadingContext lcontext)
		{
			while (true)
			{
				SkipTypeOrPack(lcontext);

				if (lcontext.Lexer.Current.Type != TokenType.Comma)
					return;

				lcontext.Lexer.Next();
			}
		}

		// ---------------------------------------------------------------
		// Local helpers - NodeBase's are instance-scoped and this is static
		// ---------------------------------------------------------------

		private static void Expect(ScriptLoadingContext lcontext, TokenType type)
		{
			Token t = lcontext.Lexer.Current;

			if (t.Type != type)
			{
				throw new SyntaxErrorException(t, "unexpected symbol near '{0}'", t.Text)
				{
					IsPrematureStreamTermination = (t.Type == TokenType.Eof)
				};
			}

			lcontext.Lexer.Next();
		}

		private static void CheckMatch(ScriptLoadingContext lcontext, Token open, TokenType type, string text)
		{
			Token t = lcontext.Lexer.Current;

			if (t.Type != type)
			{
				throw new SyntaxErrorException(t, "'{0}' expected (to close '{1}' at line {2}) near '{3}'",
					text, open.Text, open.FromLine, t.Text)
				{
					IsPrematureStreamTermination = (t.Type == TokenType.Eof)
				};
			}

			lcontext.Lexer.Next();
		}
	}
}
