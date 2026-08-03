using System.Collections.Generic;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;

namespace MoonSharp.Interpreter.Tree.Expressions
{
	/// <summary>
	/// A Luau if-then-else expression - 'if cond then a elseif cond2 then b else c'.
	/// See https://rfcs.luau.org/syntax-if-expression.html
	///
	/// Unlike the statement of the same shape it yields a value, every branch is a single
	/// expression rather than a block, and the else branch is mandatory - there is no value to
	/// produce without it. No branch can declare a local, so no scope block is involved.
	/// </summary>
	class IfExpression : Expression
	{
		List<Expression> m_Conditions = new List<Expression>();
		List<Expression> m_Values = new List<Expression>();
		Expression m_Else;

		public IfExpression(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			CheckTokenType(lcontext, TokenType.If);

			while (true)
			{
				m_Conditions.Add(Expr(lcontext));
				CheckTokenType(lcontext, TokenType.Then);
				m_Values.Add(Expr(lcontext));

				if (lcontext.Lexer.Current.Type != TokenType.ElseIf)
					break;

				lcontext.Lexer.Next();
			}

			Token t = lcontext.Lexer.Current;

			if (t.Type != TokenType.Else)
			{
				throw new SyntaxErrorException(t, "'else' expected (an if-then-else expression must have an else branch) near '{0}'", t.Text)
				{
					IsPrematureStreamTermination = (t.Type == TokenType.Eof)
				};
			}

			lcontext.Lexer.Next();

			m_Else = Expr(lcontext);
		}

		public override void Compile(ByteCode bc)
		{
			List<Instruction> endJumps = new List<Instruction>();

			for (int i = 0; i < m_Conditions.Count; i++)
			{
				m_Conditions[i].Compile(bc);
				Instruction nextCondition = bc.Emit_Jump(OpCode.Jf, -1);
				m_Values[i].Compile(bc);
				endJumps.Add(bc.Emit_Jump(OpCode.Jump, -1));
				nextCondition.NumVal = bc.GetJumpPointForNextInstruction();
			}

			m_Else.Compile(bc);

			// every branch converges here, so a single Scalar covers them all. It is needed because
			// a branch can be a call or a vararg, and the RFC makes this a single valued expression
			foreach (Instruction endJump in endJumps)
				endJump.NumVal = bc.GetJumpPointForNextInstruction();

			bc.Emit_Scalar();
		}

		public override DynValue Eval(ScriptExecutionContext context)
		{
			for (int i = 0; i < m_Conditions.Count; i++)
			{
				if (m_Conditions[i].Eval(context).ToScalar().CastToBool())
					return m_Values[i].Eval(context).ToScalar();
			}

			return m_Else.Eval(context).ToScalar();
		}
	}
}
