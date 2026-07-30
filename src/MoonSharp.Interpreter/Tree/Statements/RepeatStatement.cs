using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;


namespace MoonSharp.Interpreter.Tree.Statements
{
	class RepeatStatement : Statement
	{
		Expression m_Condition;
		Statement m_Block;
		RuntimeScopeBlock m_StackFrame;
		SourceRef m_Repeat, m_Until;

		public RepeatStatement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			m_Repeat = CheckTokenType(lcontext, TokenType.Repeat).GetSourceRef();

			lcontext.Scope.PushBlock();

			// The RFC forbids 'continue' here when the until condition reads a local declared after
			// it, since on a continued iteration that local was never assigned. Track how many
			// locals this block had declared at the earliest continue, then see whether the
			// condition reaches past that point.
			Execution.Scopes.BuildTimeScopeBlock repeatBlock = lcontext.Scope.CurrentBlock;
			int continueDefinedVars = int.MaxValue;
			Token continueToken = null;

			ParseTimeLoop parseTimeLoop = new ParseTimeLoop();
			parseTimeLoop.OnContinue = tkn =>
			{
				if (repeatBlock.DefinedCount < continueDefinedVars)
				{
					continueDefinedVars = repeatBlock.DefinedCount;
					continueToken = tkn;
				}
			};

			lcontext.PushParseTimeLoop(parseTimeLoop);
			m_Block = new CompositeStatement(lcontext);
			lcontext.PopParseTimeLoop();

			Token until = CheckTokenType(lcontext, TokenType.Until);

			if (continueToken != null)
				lcontext.Scope.BeginSymbolRecording();

			m_Condition = Expression.Expr(lcontext);

			if (continueToken != null)
			{
				foreach (SymbolRef symbol in lcontext.Scope.EndSymbolRecording())
				{
					if (repeatBlock.GetDefinitionOrdinal(symbol) >= continueDefinedVars)
					{
						throw new SyntaxErrorException(continueToken,
							"<continue> at line {0} would skip the declaration of local '{1}' used by the until condition",
							continueToken.FromLine, symbol.Name);
					}
				}
			}

			m_Until = until.GetSourceRefUpTo(lcontext.Lexer.Current);

			m_StackFrame = lcontext.Scope.PopBlock();
			lcontext.Source.Refs.Add(m_Repeat);
			lcontext.Source.Refs.Add(m_Until);
		}

		public override void Compile(ByteCode bc)
		{
			Loop L = new Loop()
			{
				Scope = m_StackFrame
			};

			bc.PushSourceRef(m_Repeat);

			bc.LoopTracker.Loops.Push(L);

			int start = bc.GetJumpPointForNextInstruction();

			bc.Emit_Enter(m_StackFrame);
			m_Block.Compile(bc);

			int continuepoint = bc.GetJumpPointForNextInstruction();

			foreach (Instruction i in L.ContinueJumps)
				i.NumVal = continuepoint;

			bc.PopSourceRef();
			bc.PushSourceRef(m_Until);
			bc.Emit_Debug("..end");

			m_Condition.Compile(bc);
			bc.Emit_Leave(m_StackFrame);
			bc.Emit_Jump(OpCode.Jf, start);

			bc.LoopTracker.Loops.Pop();

			int exitpoint = bc.GetJumpPointForNextInstruction();

			foreach (Instruction i in L.BreakJumps)
				i.NumVal = exitpoint;

			bc.PopSourceRef();
		}


	}
}
