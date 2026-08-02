using System.Collections.Generic;
using System.Text;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;

namespace MoonSharp.Interpreter.Tree.Expressions
{
	/// <summary>
	/// A Luau interpolated string - `text{expr}text`.
	/// See https://rfcs.luau.org/syntax-string-interpolation.html
	/// The lexer has already split the string into literal runs and holes, so this only has to
	/// alternate between them: there is always exactly one more literal than there are holes.
	/// </summary>
	class StringInterpolationExpression : Expression
	{
		List<string> m_Literals = new List<string>();
		List<Expression> m_Holes = new List<Expression>();

		public StringInterpolationExpression(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			Token begin = CheckTokenType(lcontext, TokenType.String_InterpBegin);
			m_Literals.Add(begin.Text);

			while (true)
			{
				m_Holes.Add(Expr(lcontext));

				Token t = lcontext.Lexer.Current;

				if (t.Type == TokenType.String_InterpMid)
				{
					m_Literals.Add(t.Text);
					lcontext.Lexer.Next();
				}
				else if (t.Type == TokenType.String_InterpEnd)
				{
					m_Literals.Add(t.Text);
					lcontext.Lexer.Next();
					return;
				}
				else
				{
					throw new SyntaxErrorException(t, "unexpected symbol near '{0}'", t.Text)
					{
						IsPrematureStreamTermination = (t.Type == TokenType.Eof)
					};
				}
			}
		}

		public override void Compile(ByteCode bc)
		{
			bc.Emit_Literal(DynValue.NewString(m_Literals[0]));

			for (int i = 0; i < m_Holes.Count; i++)
			{
				m_Holes[i].Compile(bc);

				// the RFC specifies tostring semantics for a hole, not concat semantics, so any
				// value renders rather than only strings and numbers
				bc.Emit_ToStr();
				bc.Emit_Operator(OpCode.Concat);

				bc.Emit_Literal(DynValue.NewString(m_Literals[i + 1]));
				bc.Emit_Operator(OpCode.Concat);
			}
		}

		public override DynValue Eval(ScriptExecutionContext context)
		{
			StringBuilder sb = new StringBuilder();

			sb.Append(m_Literals[0]);

			for (int i = 0; i < m_Holes.Count; i++)
			{
				DynValue v = m_Holes[i].Eval(context).ToScalar();

				sb.Append(v.ToPrintString());
				sb.Append(m_Literals[i + 1]);
			}

			return DynValue.NewString(sb.ToString());
		}
	}
}
