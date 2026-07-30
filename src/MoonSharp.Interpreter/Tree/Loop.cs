using System.Collections.Generic;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;

namespace MoonSharp.Interpreter.Tree
{
	/// <summary>
	/// Marks a loop while its body is being parsed, so a 'continue' can find the loop it belongs to
	/// before any bytecode exists. Only 'repeat' needs to react, to enforce the RFC rule that its
	/// until condition may not read a local declared after the continue.
	/// </summary>
	internal class ParseTimeLoop
	{
		public System.Action<Token> OnContinue;
	}

	internal class Loop : ILoop
	{
		public RuntimeScopeBlock Scope;
		public List<Instruction> BreakJumps = new List<Instruction>();
		public List<Instruction> ContinueJumps = new List<Instruction>();

		public void CompileBreak(ByteCode bc)
		{
			bc.Emit_Exit(Scope);
			BreakJumps.Add(bc.Emit_Jump(OpCode.Jump, -1));
		}

		public void CompileContinue(ByteCode bc)
		{
			// Jumps to the end of the loop body, so whatever the loop does between the body and
			// the next iteration still runs: the numeric increment, the until condition, and the
			// loop's own Emit_Leave. Only the locals of blocks nested inside the loop are cleared
			// here, which is what Emit_Clean covers, since the loop's own Emit_Leave is still ahead.
			bc.Emit_Clean(Scope);
			ContinueJumps.Add(bc.Emit_Jump(OpCode.Jump, -1));
		}

		public bool IsBoundary()
		{
			return false;
		}
	}

	internal class LoopBoundary : ILoop
	{
		public void CompileBreak(ByteCode bc)
		{
			throw new InternalErrorException("CompileBreak called on LoopBoundary");
		}

		public void CompileContinue(ByteCode bc)
		{
			throw new InternalErrorException("CompileContinue called on LoopBoundary");
		}

		public bool IsBoundary()
		{
			return true;
		}
	}

}
