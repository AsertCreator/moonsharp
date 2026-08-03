using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
	/// <summary>
	/// Tests for the Luau if-then-else expression.
	/// See https://rfcs.luau.org/syntax-if-expression.html
	/// </summary>
	[TestFixture]
	public class IfExpressionTests
	{
		private static Script NewScript()
		{
			Script s = new Script();
			s.Options.LuauFeatures = LuauFeatures.IfExpression;
			return s;
		}

		private static DynValue Run(string script)
		{
			return NewScript().DoString(script);
		}

		private static void AssertNumber(double expected, string script)
		{
			DynValue res = Run(script);
			Assert.AreEqual(DataType.Number, res.Type);
			Assert.AreEqual(expected, res.Number);
		}

		private static void AssertString(string expected, string script)
		{
			DynValue res = Run(script);
			Assert.AreEqual(DataType.String, res.Type);
			Assert.AreEqual(expected, res.String);
		}

		// ---------------------------------------------------------------
		// Both branches
		// ---------------------------------------------------------------

		[Test]
		public void IfExpression_TakesThenBranch()
		{
			AssertNumber(1, "local x = if true then 1 else 2; return x;");
		}

		[Test]
		public void IfExpression_TakesElseBranch()
		{
			AssertNumber(2, "local x = if false then 1 else 2; return x;");
		}

		[Test]
		public void IfExpression_ConditionIsAnExpression()
		{
			AssertNumber(-1, "local n = -5; return if n < 0 then -1 else 1;");
		}

		// ---------------------------------------------------------------
		// elseif chains
		// ---------------------------------------------------------------

		[Test]
		public void IfExpression_ElseIf_FirstMatch()
		{
			AssertString("neg", "local n = -5; return if n < 0 then 'neg' elseif n > 0 then 'pos' else 'zero';");
		}

		[Test]
		public void IfExpression_ElseIf_SecondMatch()
		{
			AssertString("pos", "local n = 5; return if n < 0 then 'neg' elseif n > 0 then 'pos' else 'zero';");
		}

		[Test]
		public void IfExpression_ElseIf_FallsThroughToElse()
		{
			AssertString("zero", "local n = 0; return if n < 0 then 'neg' elseif n > 0 then 'pos' else 'zero';");
		}

		[Test]
		public void IfExpression_LongElseIfChain()
		{
			AssertNumber(4, @"
				local n = 4
				return if n == 1 then 1
					elseif n == 2 then 2
					elseif n == 3 then 3
					elseif n == 4 then 4
					elseif n == 5 then 5
					else 0;");
		}

		// ---------------------------------------------------------------
		// Lua truthiness, not C# truthiness
		// ---------------------------------------------------------------

		[Test]
		public void IfExpression_NilIsFalsy()
		{
			AssertNumber(2, "return if nil then 1 else 2;");
		}

		[Test]
		public void IfExpression_ZeroIsTruthy()
		{
			AssertNumber(1, "return if 0 then 1 else 2;");
		}

		[Test]
		public void IfExpression_EmptyStringIsTruthy()
		{
			AssertNumber(1, "return if '' then 1 else 2;");
		}

		// ---------------------------------------------------------------
		// Only the branch that is taken runs
		// ---------------------------------------------------------------

		[Test]
		public void IfExpression_UntakenBranchIsNotEvaluated()
		{
			AssertNumber(10, @"
				local calls = 0
				local function bump() calls = calls + 1; return 0 end
				local x = if true then 10 else bump()
				return x + calls;");
		}

		[Test]
		public void IfExpression_UntakenThenBranchIsNotEvaluated()
		{
			AssertNumber(20, @"
				local calls = 0
				local function bump() calls = calls + 1; return 0 end
				local x = if false then bump() else 20
				return x + calls;");
		}

		[Test]
		public void IfExpression_LaterConditionsAreNotEvaluatedOnceOneMatches()
		{
			AssertNumber(1, @"
				local calls = 0
				local function cond() calls = calls + 1; return true end
				local x = if true then 1 elseif cond() then 2 else 3
				return x + calls;");
		}

		[Test]
		public void IfExpression_ConditionIsEvaluatedOnce()
		{
			AssertNumber(1, @"
				local calls = 0
				local function cond() calls = calls + 1; return true end
				local x = if cond() then 1 else 2
				return calls;");
		}

		// ---------------------------------------------------------------
		// It is an expression, so it goes anywhere a value goes
		// ---------------------------------------------------------------

		[Test]
		public void IfExpression_AsFunctionArgument()
		{
			AssertNumber(7, @"
				local function id(v) return v end
				return id(if true then 7 else 8);");
		}

		[Test]
		public void IfExpression_AsTableConstructorValue()
		{
			AssertNumber(7, "local t = { v = if true then 7 else 8 }; return t.v;");
		}

		[Test]
		public void IfExpression_AsTableConstructorArrayItem()
		{
			AssertNumber(7, "local t = { if true then 7 else 8 }; return t[1];");
		}

		[Test]
		public void IfExpression_AsTableIndex()
		{
			AssertNumber(20, "local t = { 10, 20 }; return t[if true then 2 else 1];");
		}

		[Test]
		public void IfExpression_AsReturnValue()
		{
			AssertNumber(3, @"
				local function pick(b) return if b then 3 else 4 end
				return pick(true);");
		}

		[Test]
		public void IfExpression_InAWhileCondition()
		{
			AssertNumber(3, @"
				local i = 0
				while if i < 3 then true else false do
					i = i + 1
				end
				return i;");
		}

		[Test]
		public void IfExpression_AsAssignmentToAField()
		{
			AssertNumber(7, "local t = {}; t.v = if true then 7 else 8; return t.v;");
		}

		[Test]
		public void IfExpression_InAnExpressionList()
		{
			AssertNumber(12, @"
				local a, b = if true then 5 else 0, if false then 0 else 7
				return a + b;");
		}

		// ---------------------------------------------------------------
		// Nesting
		// ---------------------------------------------------------------

		[Test]
		public void IfExpression_NestedInThenBranch()
		{
			AssertNumber(2, "return if true then (if false then 1 else 2) else 3;");
		}

		[Test]
		public void IfExpression_NestedInElseBranchWithoutParentheses()
		{
			AssertNumber(3, "return if false then 1 else if false then 2 else 3;");
		}

		[Test]
		public void IfExpression_NestedInCondition()
		{
			AssertNumber(1, "return if (if true then true else false) then 1 else 2;");
		}

		// ---------------------------------------------------------------
		// Precedence: the branches are full expressions, so they bind greedily
		// ---------------------------------------------------------------

		[Test]
		public void IfExpression_ThenBranchIsAWholeExpression()
		{
			AssertNumber(6, "return if true then 2 * 3 else 0;");
		}

		[Test]
		public void IfExpression_ElseBranchIsAWholeExpression()
		{
			AssertNumber(6, "return if false then 0 else 2 * 3;");
		}

		[Test]
		public void IfExpression_ElseBranchSwallowsTrailingOperators()
		{
			// the whole if-expression sits lowest in precedence, so '2 + 3' is the else branch
			// rather than the if-expression being an operand of '+'
			AssertNumber(5, "return if false then 1 else 2 + 3;");
		}

		[Test]
		public void IfExpression_ElseBranchSwallowsTrailingLogicalOperators()
		{
			AssertNumber(2, "return if false then 1 else 2 or 3;");
		}

		[Test]
		public void IfExpression_InParenthesesDoesNotSwallowTrailingOperators()
		{
			AssertNumber(11, "return (if true then 1 else 2) + 10;");
		}

		[Test]
		public void IfExpression_AsRightHandOperand()
		{
			// 1 + (if true then 2 else 3 * 2), the else branch still binds greedily
			AssertNumber(3, "return 1 + if true then 2 else 3 * 2;");
		}

		[Test]
		public void IfExpression_ConcatenationInBranches()
		{
			AssertString("ab", "return if true then 'a' .. 'b' else 'c';");
		}

		// ---------------------------------------------------------------
		// It yields exactly one value
		// ---------------------------------------------------------------

		[Test]
		public void IfExpression_AdjustsAMultipleReturnToOneValue()
		{
			AssertString("1|nil", @"
				local function two() return 1, 2 end
				local a, b = if true then two() else 0
				return tostring(a) .. '|' .. tostring(b);");
		}

		[Test]
		public void IfExpression_AdjustsVarargsToOneValue()
		{
			AssertNumber(1, @"
				local function f(...)
					return select('#', if true then ... else nil)
				end
				return f(10, 20, 30);");
		}

		// ---------------------------------------------------------------
		// The statement form is untouched
		// ---------------------------------------------------------------

		[Test]
		public void IfStatement_StillParsesWhenIfExpressionsAreEnabled()
		{
			AssertNumber(3, @"
				local x = 0
				if false then x = 1 elseif true then x = 3 else x = 2 end
				return x;");
		}

		[Test]
		public void IfStatement_ContainingAnIfExpression()
		{
			AssertNumber(7, @"
				local x = 0
				if true then x = if true then 7 else 8 end
				return x;");
		}

		// ---------------------------------------------------------------
		// It survives a binary dump, since it emits jumps outside a statement
		// ---------------------------------------------------------------

		[Test]
		public void IfExpression_SurvivesBinaryDumpRoundTrip()
		{
			string script = @"
				local function classify(n)
					return if n < 0 then 'neg' elseif n > 0 then 'pos' else 'zero'
				end
				return classify(-1) .. classify(1) .. classify(0);";

			Script s1 = NewScript();
			DynValue v1 = s1.LoadString(script);

			using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
			{
				s1.Dump(v1, ms);
				ms.Seek(0, System.IO.SeekOrigin.Begin);

				// the dump is already compiled, so the reader needs no Luau features enabled
				Script s2 = new Script();
				DynValue res = s2.LoadStream(ms).Function.Call();

				Assert.AreEqual(DataType.String, res.Type);
				Assert.AreEqual("negposzero", res.String);
			}
		}

		// ---------------------------------------------------------------
		// Rejected shapes
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void IfExpression_WithoutElse_IsSyntaxError()
		{
			Run("local x = if true then 1");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void IfExpression_WithoutElseButWithEnd_IsSyntaxError()
		{
			Run("local x = if true then 1 end");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void IfExpression_ElseIfWithoutElse_IsSyntaxError()
		{
			Run("local x = if false then 1 elseif true then 2");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void IfExpression_WithoutThen_IsSyntaxError()
		{
			Run("local x = if true 1 else 2");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void IfExpression_EmptyThenBranch_IsSyntaxError()
		{
			Run("local x = if true then else 2");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void IfExpression_AsAStatement_IsSyntaxError()
		{
			// 'if' at statement position is still an if-statement, which needs 'end'
			Run("if true then 1 else 2");
		}
	}
}
