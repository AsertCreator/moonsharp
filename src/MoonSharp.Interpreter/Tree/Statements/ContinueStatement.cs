using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;


namespace MoonSharp.Interpreter.Tree.Statements
{
	/// <summary>
	/// The Luau 'continue' statement - see https://rfcs.luau.org/syntax-continue-statement.html
	/// 'continue' is context sensitive rather than reserved, so the decision to parse one is taken
	/// in Statement.CreateStatement; by the time we get here it is definitely a statement.
	/// </summary>
	class ContinueStatement : Statement
	{
		SourceRef m_Ref;

		public ContinueStatement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			Token tkn = CheckTokenType(lcontext, TokenType.Name);

			m_Ref = tkn.GetSourceRef();
			lcontext.Source.Refs.Add(m_Ref);

			ParseTimeLoop loop = lcontext.CurrentParseTimeLoop;

			if (loop != null && loop.OnContinue != null)
				loop.OnContinue(tkn);
		}

		public override void Compile(ByteCode bc)
		{
			using (bc.EnterSource(m_Ref))
			{
				if (bc.LoopTracker.Loops.Count == 0)
					throw new SyntaxErrorException(this.Script, m_Ref, "<continue> at line {0} not inside a loop", m_Ref.FromLine);

				ILoop loop = bc.LoopTracker.Loops.Peek();

				if (loop.IsBoundary())
					throw new SyntaxErrorException(this.Script, m_Ref, "<continue> at line {0} not inside a loop", m_Ref.FromLine);

				loop.CompileContinue(bc);
			}
		}
	}
}
