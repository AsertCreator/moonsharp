using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;

namespace MoonSharp.Interpreter.Tree.Statements
{
	/// <summary>
	/// A Luau type alias - 'type Point = { x: number, y: number }', optionally exported.
	/// See https://luau.org/typecheck
	///
	/// MoonSharp has no type checker, so the alias is validated for syntax and then dropped: it
	/// declares no name, emits no code, and is invisible to everything downstream.
	/// </summary>
	class TypeAliasStatement : Statement
	{
		/// <summary>
		/// Neither 'type' nor 'export' is a reserved word - 'type' is a global function in every
		/// Lua, and 'export' is an ordinary name - so both stay usable as identifiers and this
		/// only claims the statement when what follows cannot be anything else.
		/// </summary>
		public static bool IsTypeAlias(ScriptLoadingContext lcontext, Token tkn)
		{
			if (!TypeAnnotation.IsEnabled(lcontext))
				return false;

			// 'type X ...' - a call is 'type(x)' and an assignment is 'type = x', neither of
			// which has a name next
			if (tkn.Text == "type")
				return lcontext.Lexer.PeekNext().Type == TokenType.Name;

			if (tkn.Text == "export")
				return lcontext.Lexer.PeekNext().Text == "type";

			return false;
		}

		public TypeAliasStatement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			if (lcontext.Lexer.Current.Text == "export")
				lcontext.Lexer.Next();

			lcontext.Lexer.Next();   // the 'type' itself

			CheckTokenType(lcontext, TokenType.Name);

			TypeAnnotation.SkipOptionalGenericParams(lcontext);

			CheckTokenType(lcontext, TokenType.Op_Assignment);

			TypeAnnotation.SkipType(lcontext);
		}

		public override void Compile(ByteCode bc)
		{
		}
	}
}
