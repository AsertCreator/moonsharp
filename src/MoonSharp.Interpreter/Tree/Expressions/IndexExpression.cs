using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;

namespace MoonSharp.Interpreter.Tree.Expressions
{
	class IndexExpression : Expression, IVariable
	{
		Expression m_BaseExp;
		Expression m_IndexExp;
		string m_Name;


		public IndexExpression(Expression baseExp, Expression indexExp, ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			m_BaseExp = baseExp;
			m_IndexExp = indexExp;
		}

		public IndexExpression(Expression baseExp, string name, ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			m_BaseExp = baseExp;
			m_Name = name;
		}


		public override void Compile(ByteCode bc)
		{
			m_BaseExp.Compile(bc);

			if (m_Name != null)
			{
				bc.Emit_Index(DynValue.NewString(m_Name), true);
			}
			else if (m_IndexExp is LiteralExpression)
			{
				LiteralExpression lit = (LiteralExpression)m_IndexExp;
				bc.Emit_Index(lit.Value);
			}
			else
			{
				m_IndexExp.Compile(bc);
				bc.Emit_Index(isExpList: (m_IndexExp is ExprListExpression));
			}
		}

		public void CompileAssignment(ByteCode bc, int stackofs, int tupleidx)
		{
			m_BaseExp.Compile(bc);

			if (m_Name != null)
			{
				bc.Emit_IndexSet(stackofs, tupleidx, DynValue.NewString(m_Name), isNameIndex: true);
			}
			else if (m_IndexExp is LiteralExpression)
			{
				LiteralExpression lit = (LiteralExpression)m_IndexExp;
				bc.Emit_IndexSet(stackofs, tupleidx, lit.Value);
			}
			else
			{
				m_IndexExp.Compile(bc);
				bc.Emit_IndexSet(stackofs, tupleidx, isExpList: (m_IndexExp is ExprListExpression));
			}
		}

		public void CompileCompoundAssignment(ByteCode bc, Expression rvalue, OpCode op)
		{
			// The base and the key must be evaluated exactly once - 't[f()] += 1' calls f once - so
			// they are computed, kept on the stack, and duplicated for the read. See
			// https://rfcs.luau.org/syntax-compound-assignment.html
			DynValue embeddedIndex = null;
			bool isNameIndex = false;
			bool isExpList = false;

			if (m_Name != null)
			{
				embeddedIndex = DynValue.NewString(m_Name);
				isNameIndex = true;
			}
			else if (m_IndexExp is LiteralExpression)
			{
				embeddedIndex = ((LiteralExpression)m_IndexExp).Value;
			}
			else
			{
				isExpList = m_IndexExp is ExprListExpression;
			}

			// A key that is embedded in the instruction itself costs no stack slot.
			bool keyOnStack = (embeddedIndex == null);

			m_BaseExp.Compile(bc);                                  // [ base ]

			if (keyOnStack)
			{
				m_IndexExp.Compile(bc);                             // [ base, key ]
				bc.Emit_Copy(1);                                    // [ base, key, base ]
				bc.Emit_Copy(1);                                    // [ base, key, base, key ]
			}
			else
			{
				bc.Emit_Copy(0);                                    // [ base, base ]
			}

			bc.Emit_Index(embeddedIndex, isNameIndex, isExpList);   // [ base, (key,) value ]

			rvalue.Compile(bc);                                     // [ base, (key,) value, rvalue ]
			bc.Emit_Operator(op);                                   // [ base, (key,) result ]

			// Rotate the base (and key) back to the top so Emit_IndexSet can pop them, leaving the
			// result immediately below - i.e. at stack offset 0, where the store reads its value from.
			if (keyOnStack)
			{
				bc.Emit_Swap(0, 2);                                 // [ result, key, base ]
				bc.Emit_Swap(0, 1);                                 // [ result, base, key ]
			}
			else
			{
				bc.Emit_Swap(0, 1);                                 // [ result, base ]
			}

			bc.Emit_IndexSet(0, 0, embeddedIndex, isNameIndex, isExpList);
			bc.Emit_Pop();
		}

		public override DynValue Eval(ScriptExecutionContext context)
		{
			DynValue b = m_BaseExp.Eval(context).ToScalar();
			DynValue i = m_IndexExp != null ? m_IndexExp.Eval(context).ToScalar() : DynValue.NewString(m_Name);

			if (b.Type != DataType.Table) throw new DynamicExpressionException("Attempt to index non-table.");
			else if (i.IsNilOrNan()) throw new DynamicExpressionException("Attempt to index with nil or nan key.");
			return b.Table.Get(i) ?? DynValue.Nil;
		}
	}
}
