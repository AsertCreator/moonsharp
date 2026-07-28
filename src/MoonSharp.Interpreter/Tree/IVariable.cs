
namespace MoonSharp.Interpreter.Tree
{
	interface IVariable
	{
		void CompileAssignment(Execution.VM.ByteCode bc, int stackofs, int tupleidx);

		/// <summary>
		/// Emits a read-modify-write of this l-value: 'lvalue = lvalue op rvalue'.
		/// Implementers must evaluate their own subexpressions exactly once, as required by
		/// https://rfcs.luau.org/syntax-compound-assignment.html - so this cannot simply be
		/// expressed as Compile() followed by CompileAssignment(), which would evaluate them twice.
		/// Must leave the stack balanced.
		/// </summary>
		void CompileCompoundAssignment(Execution.VM.ByteCode bc, Expression rvalue, Execution.VM.OpCode op);
	}
}
