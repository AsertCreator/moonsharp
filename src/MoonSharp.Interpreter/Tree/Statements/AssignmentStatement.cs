using System;
using System.Collections.Generic;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Execution;

using MoonSharp.Interpreter.Tree.Expressions;

namespace MoonSharp.Interpreter.Tree.Statements
{
	class AssignmentStatement : Statement
	{
		List<IVariable> m_LValues = new List<IVariable>();
		List<Expression> m_RValues;
		SourceRef m_Ref;

		// Set only for compound assignment ('x += 1'), in which case there is exactly one l-value
		// and exactly one r-value. Null for a plain assignment.
		Execution.VM.OpCode? m_CompoundOperator;


		public AssignmentStatement(ScriptLoadingContext lcontext, Token startToken)
			: base(lcontext)
		{
			List<string> names = new List<string>();

			Token first = startToken;

			while (true)
			{
				Token name = CheckTokenType(lcontext, TokenType.Name);
				names.Add(name.Text);

				TypeAnnotation.SkipOptionalAnnotation(lcontext);

				if (lcontext.Lexer.Current.Type != TokenType.Comma)
					break;

				lcontext.Lexer.Next();
			}

			if (lcontext.Lexer.Current.IsCompoundAssignmentOperator())
			{
				throw new SyntaxErrorException(lcontext.Lexer.Current,
					"unexpected symbol near '{0}' - compound assignment cannot be used to declare a local",
					lcontext.Lexer.Current.Text);
			}

			if (lcontext.Lexer.Current.Type == TokenType.Op_Assignment)
			{
				CheckTokenType(lcontext, TokenType.Op_Assignment);
				m_RValues = Expression.ExprList(lcontext);
			}
			else
			{
				m_RValues = new List<Expression>();
			}

			foreach (string name in names)
			{
				var localVar = lcontext.Scope.TryDefineLocal(name);
				var symbol = new SymbolRefExpression(lcontext, localVar);
				m_LValues.Add(symbol);
			}

			Token last = lcontext.Lexer.Current;
			m_Ref = first.GetSourceRefUpTo(last);
			lcontext.Source.Refs.Add(m_Ref);

		}


		public AssignmentStatement(ScriptLoadingContext lcontext, Expression firstExpression, Token first)
			: base(lcontext)
		{
			m_LValues.Add(CheckVar(lcontext, firstExpression));

			if (lcontext.Lexer.Current.IsCompoundAssignmentOperator())
			{
				ParseCompoundAssignment(lcontext, first);
				return;
			}

			while (lcontext.Lexer.Current.Type == TokenType.Comma)
			{
				lcontext.Lexer.Next();
				Expression e = Expression.PrimaryExp(lcontext);
				m_LValues.Add(CheckVar(lcontext, e));
			}

			if (lcontext.Lexer.Current.IsCompoundAssignmentOperator())
			{
				throw new SyntaxErrorException(lcontext.Lexer.Current,
					"unexpected symbol near '{0}' - compound assignment supports only a single target",
					lcontext.Lexer.Current.Text);
			}

			CheckTokenType(lcontext, TokenType.Op_Assignment);

			m_RValues = Expression.ExprList(lcontext);

			Token last = lcontext.Lexer.Current;
			m_Ref = first.GetSourceRefUpTo(last);
			lcontext.Source.Refs.Add(m_Ref);

		}

		/// <summary>
		/// Parses the tail of a compound assignment ('x += 1'), starting at the operator.
		/// Exactly one target and exactly one value - see
		/// https://rfcs.luau.org/syntax-compound-assignment.html
		/// </summary>
		private void ParseCompoundAssignment(ScriptLoadingContext lcontext, Token first)
		{
			Token op = lcontext.Lexer.Current;
			m_CompoundOperator = CompoundOperatorToOpCode(op);
			lcontext.Lexer.Next();

			m_RValues = new List<Expression>() { Expression.Expr(lcontext) };

			if (lcontext.Lexer.Current.Type == TokenType.Comma)
			{
				throw new SyntaxErrorException(lcontext.Lexer.Current,
					"unexpected symbol near ',' - compound assignment supports only a single value");
			}

			Token last = lcontext.Lexer.Current;
			m_Ref = first.GetSourceRefUpTo(last);
			lcontext.Source.Refs.Add(m_Ref);
		}

		private static Execution.VM.OpCode CompoundOperatorToOpCode(Token op)
		{
			switch (op.Type)
			{
				case TokenType.Op_AddAssign:
					return Execution.VM.OpCode.Add;
				case TokenType.Op_SubAssign:
					return Execution.VM.OpCode.Sub;
				case TokenType.Op_MulAssign:
					return Execution.VM.OpCode.Mul;
				case TokenType.Op_DivAssign:
					return Execution.VM.OpCode.Div;
				case TokenType.Op_FloorDivAssign:
					return Execution.VM.OpCode.FloorDiv;
				case TokenType.Op_ModAssign:
					return Execution.VM.OpCode.Mod;
				case TokenType.Op_PwrAssign:
					return Execution.VM.OpCode.Power;
				case TokenType.Op_ConcatAssign:
					return Execution.VM.OpCode.Concat;
				default:
					throw new InternalErrorException("Unexpected compound assignment operator {0}", op.Type);
			}
		}

		private IVariable CheckVar(ScriptLoadingContext lcontext, Expression firstExpression)
		{
			IVariable v = firstExpression as IVariable;

			if (v == null)
				throw new SyntaxErrorException(lcontext.Lexer.Current, "unexpected symbol near '{0}' - not a l-value", lcontext.Lexer.Current);

			return v;
		}


		public override void Compile(Execution.VM.ByteCode bc)
		{
			using (bc.EnterSource(m_Ref))
			{
				if (m_CompoundOperator.HasValue)
				{
					m_LValues[0].CompileCompoundAssignment(bc, m_RValues[0], m_CompoundOperator.Value);
					return;
				}

				foreach (var exp in m_RValues)
				{
					exp.Compile(bc);
				}

				for (int i = 0; i < m_LValues.Count; i++)
					m_LValues[i].CompileAssignment(bc,
							Math.Max(m_RValues.Count - 1 - i, 0), // index of r-value
							i - Math.Min(i, m_RValues.Count - 1)); // index in last tuple

				bc.Emit_Pop(m_RValues.Count);
			}
		}

	}
}
