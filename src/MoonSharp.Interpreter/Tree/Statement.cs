using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Tree.Expressions;
using MoonSharp.Interpreter.Tree.Statements;

namespace MoonSharp.Interpreter.Tree
{
	abstract class Statement : NodeBase
	{
		public Statement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{ }


		protected static Statement CreateStatement(ScriptLoadingContext lcontext, out bool forceLast)
		{
			Token tkn = lcontext.Lexer.Current;

			forceLast = false;

			switch (tkn.Type)
			{
				case TokenType.DoubleColon:
					return new LabelStatement(lcontext);
				case TokenType.Goto:
					return new GotoStatement(lcontext);
				case TokenType.SemiColon:
					lcontext.Lexer.Next();
					return new EmptyStatement(lcontext);
				case TokenType.If:
					return new IfStatement(lcontext);
				case TokenType.While:
					return new WhileStatement(lcontext);
				case TokenType.Do:
					return new ScopeBlockStatement(lcontext);
				case TokenType.For:
					return DispatchForLoopStatement(lcontext);
				case TokenType.Repeat:
					return new RepeatStatement(lcontext);
				case TokenType.Function:
					return new FunctionDefinitionStatement(lcontext, false, null);
				case TokenType.Local:
					Token localToken = lcontext.Lexer.Current;
					lcontext.Lexer.Next();
					if (lcontext.Lexer.Current.Type == TokenType.Function)
						return new FunctionDefinitionStatement(lcontext, true, localToken);
					else
						return new AssignmentStatement(lcontext, localToken);
				case TokenType.Return:
					forceLast = true;
					return new ReturnStatement(lcontext);
				case TokenType.Break:
					return new BreakStatement(lcontext);
				case TokenType.Name:
					if (IsContinueStatement(lcontext, tkn))
					{
						forceLast = true;
						return new ContinueStatement(lcontext);
					}
					if (TypeAliasStatement.IsTypeAlias(lcontext, tkn))
						return new TypeAliasStatement(lcontext);
					goto default;
				default:
					{
						Token l = lcontext.Lexer.Current;
						Expression exp = Expression.PrimaryExp(lcontext);
						FunctionCallExpression fnexp = exp as FunctionCallExpression;

						if (fnexp != null)
							return new FunctionCallStatement(lcontext, fnexp);
						else
							return new AssignmentStatement(lcontext, exp, l);
					}
			}
		}

		/// <summary>
		/// 'continue' is a context sensitive keyword, not a reserved one, so 'continue' remains a
		/// perfectly good variable name. It is only a statement when the token after it cannot
		/// continue an expression - see https://rfcs.luau.org/syntax-continue-statement.html
		/// </summary>
		private static bool IsContinueStatement(ScriptLoadingContext lcontext, Token tkn)
		{
			if (tkn.Text != "continue")
				return false;

			if ((lcontext.Script.Options.LuauFeatures & LuauFeatures.ContinueStatement) == 0)
				return false;

			Token next = lcontext.Lexer.PeekNext();

			if (next.IsCompoundAssignmentOperator())
				return false;

			switch (next.Type)
			{
				case TokenType.Dot:
				case TokenType.Brk_Open_Square:
				case TokenType.Colon:
				case TokenType.Brk_Open_Curly:
				case TokenType.Brk_Open_Curly_Shared:
				case TokenType.Brk_Open_Round:
				case TokenType.Op_Assignment:
				case TokenType.String:
				case TokenType.String_Long:
				case TokenType.Comma:
					return false;
				default:
					return true;
			}
		}

		private static Statement DispatchForLoopStatement(ScriptLoadingContext lcontext)
		{
			//	for Name ‘=’ exp ‘,’ exp [‘,’ exp] do block end | 
			//	for namelist in explist do block end | 		

			Token forTkn = CheckTokenType(lcontext, TokenType.For);

			Token name = CheckTokenType(lcontext, TokenType.Name);

			// 'for i: number = 1, 10' and 'for k: string, v in ...' both annotate the first name
			// here, before the '=' vs ',' decides which kind of loop this is
			TypeAnnotation.SkipOptionalAnnotation(lcontext);

			if (lcontext.Lexer.Current.Type == TokenType.Op_Assignment)
				return new ForLoopStatement(lcontext, name, forTkn);
			else
				return new ForEachLoopStatement(lcontext, name, forTkn);
		}




	}



}
