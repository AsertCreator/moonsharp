using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
	/// <summary>
	/// Tests for the Luau floor division operator '//' and its compound form '//='.
	/// See https://rfcs.luau.org/syntax-floor-division-operator.html
	/// </summary>
	[TestFixture]
	public class FloorDivisionTests
	{
		private static Script NewScript()
		{
			Script s = new Script();
			s.Options.LuauFeatures = LuauFeatures.FloorDivision | LuauFeatures.CompoundAssignment;
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

		// ---------------------------------------------------------------
		// The operator itself
		// ---------------------------------------------------------------

		[Test]
		public void FloorDiv_Truncates()
		{
			AssertNumber(3, "return 7 // 2;");
		}

		[Test]
		public void FloorDiv_ExactDivision()
		{
			AssertNumber(2, "return 6 // 3;");
		}

		[Test]
		public void FloorDiv_IsNotPlainDivision()
		{
			AssertNumber(3.5, "return 7 / 2;");
		}

		[Test]
		public void FloorDiv_FractionalOperands()
		{
			AssertNumber(3, "return 7.5 // 2;");
		}

		[Test]
		public void FloorDiv_FractionalDivisor()
		{
			AssertNumber(4, "return 2 // 0.5;");
		}

		// ---------------------------------------------------------------
		// It floors, it does not truncate towards zero
		// ---------------------------------------------------------------

		[Test]
		public void FloorDiv_NegativeDividend_RoundsDown()
		{
			// -3.5 floors to -4, it does not truncate to -3
			AssertNumber(-4, "return -7 // 2;");
		}

		[Test]
		public void FloorDiv_NegativeDivisor_RoundsDown()
		{
			AssertNumber(-4, "return 7 // -2;");
		}

		[Test]
		public void FloorDiv_BothNegative()
		{
			AssertNumber(3, "return -7 // -2;");
		}

		[Test]
		public void FloorDiv_NegativeExactDivision()
		{
			AssertNumber(-2, "return -6 // 3;");
		}

		// ---------------------------------------------------------------
		// Division by zero yields an infinity, as plain division does,
		// since every MoonSharp number is a double
		// ---------------------------------------------------------------

		[Test]
		public void FloorDiv_ByZero_IsPositiveInfinity()
		{
			DynValue res = Run("return 7 // 0 == 1 / 0;");
			Assert.AreEqual(true, res.Boolean);
		}

		[Test]
		public void FloorDiv_NegativeByZero_IsNegativeInfinity()
		{
			DynValue res = Run("return -7 // 0 == -1 / 0;");
			Assert.AreEqual(true, res.Boolean);
		}

		[Test]
		public void FloorDiv_ZeroByZero_IsNaN()
		{
			DynValue res = Run("local x = 0 // 0; return x ~= x;");
			Assert.AreEqual(true, res.Boolean);
		}

		// ---------------------------------------------------------------
		// Precedence: same level as '*', '/' and '%', left associative,
		// below unary minus and below '^'
		// ---------------------------------------------------------------

		[Test]
		public void FloorDiv_BindsTighterThanAddition()
		{
			AssertNumber(2, "return 1 + 6 // 4;");
		}

		[Test]
		public void FloorDiv_IsLeftAssociativeWithMultiplication()
		{
			// (2 * 6) // 4, not 2 * (6 // 4)
			AssertNumber(3, "return 2 * 6 // 4;");
		}

		[Test]
		public void FloorDiv_IsLeftAssociativeWithItself()
		{
			// (100 // 7) // 2 is 7, 100 // (7 // 2) would be 33
			AssertNumber(7, "return 100 // 7 // 2;");
		}

		[Test]
		public void FloorDiv_BindsLooserThanPower()
		{
			// (2 ^ 2) // 3
			AssertNumber(1, "return 2 ^ 2 // 3;");
		}

		[Test]
		public void FloorDiv_BindsLooserThanUnaryMinus()
		{
			// (-7) // 2, not -(7 // 2) which would be -3
			AssertNumber(-4, "local a = 7; return -a // 2;");
		}

		[Test]
		public void FloorDiv_MixesWithModulo()
		{
			AssertNumber(1, "return 7 // 2 % 2;");
		}

		// ---------------------------------------------------------------
		// Coercion and metamethods behave as they do for '/'
		// ---------------------------------------------------------------

		[Test]
		public void FloorDiv_CoercesNumericStrings()
		{
			AssertNumber(3, "return '7' // 2;");
		}

		[Test]
		public void FloorDiv_UsesIdivMetamethod()
		{
			DynValue res = Run(@"
				local mt = { __idiv = function(a, b) return 'idiv' end }
				local x = setmetatable({}, mt)
				return x // 2;");
			Assert.AreEqual("idiv", res.String);
		}

		[Test]
		public void FloorDiv_UsesIdivMetamethodOnTheRightOperand()
		{
			DynValue res = Run(@"
				local mt = { __idiv = function(a, b) return 'idiv' end }
				local x = setmetatable({}, mt)
				return 2 // x;");
			Assert.AreEqual("idiv", res.String);
		}

		[Test]
		public void FloorDiv_DoesNotUseDivMetamethod()
		{
			DynValue res = Run(@"
				local mt = { __div = function(a, b) return 'div' end, __idiv = function(a, b) return 'idiv' end }
				local x = setmetatable({}, mt)
				return (x // 2) .. (x / 2);");
			Assert.AreEqual("idivdiv", res.String);
		}

		[Test]
		[ExpectedException(typeof(ScriptRuntimeException))]
		public void FloorDiv_OnNonNumberWithoutMetamethod_IsRuntimeError()
		{
			Run("return {} // 2;");
		}

		// ---------------------------------------------------------------
		// The compound form
		// ---------------------------------------------------------------

		[Test]
		public void FloorDivAssign_Local()
		{
			AssertNumber(3, "local x = 7; x //= 2; return x;");
		}

		[Test]
		public void FloorDivAssign_Global()
		{
			AssertNumber(3, "x = 7; x //= 2; return x;");
		}

		[Test]
		public void FloorDivAssign_Upvalue()
		{
			AssertNumber(3, @"
				local x = 7
				local function halve() x //= 2 end
				halve()
				return x;");
		}

		[Test]
		public void FloorDivAssign_TableField()
		{
			AssertNumber(3, "local t = { a = 7 }; t.a //= 2; return t.a;");
		}

		[Test]
		public void FloorDivAssign_TableIndex()
		{
			AssertNumber(3, "local t = { 7 }; t[1] //= 2; return t[1];");
		}

		[Test]
		public void FloorDivAssign_RightHandSideIsWholeExpression()
		{
			// x = 20 // (2 + 3), not (20 // 2) + 3
			AssertNumber(4, "local x = 20; x //= 2 + 3; return x;");
		}

		[Test]
		public void FloorDivAssign_EvaluatesKeyExpressionOnce()
		{
			AssertNumber(1, @"
				local t = { 7 }
				local calls = 0
				local function key()
					calls = calls + 1
					return 1
				end
				t[key()] //= 2
				return calls;");
		}

		[Test]
		public void FloorDivAssign_UsesIdivMetamethod()
		{
			DynValue res = Run(@"
				local mt = { __idiv = function(a, b) return 'idiv' end }
				local x = setmetatable({}, mt)
				x //= 2
				return x;");
			Assert.AreEqual("idiv", res.String);
		}

		[Test]
		public void FloorDivAssign_InALoop()
		{
			// 100 -> 33 -> 11 -> 3
			AssertNumber(3, @"
				local x = 100
				for i = 1, 3 do
					x //= 3
				end
				return x;");
		}

		// ---------------------------------------------------------------
		// The dynamic expression evaluator takes its own path through the tree
		// ---------------------------------------------------------------

		[Test]
		public void FloorDiv_InADynamicExpression()
		{
			Script s = NewScript();
			s.DoString("a = 7");
			DynValue res = s.CreateDynamicExpression("a // 2").Evaluate();
			Assert.AreEqual(DataType.Number, res.Type);
			Assert.AreEqual(3, res.Number);
		}

		// ---------------------------------------------------------------
		// Two slashes elsewhere in the source are still not an operator
		// ---------------------------------------------------------------

		[Test]
		public void FloorDiv_InsideAStringIsJustText()
		{
			DynValue res = Run("return 'a//b';");
			Assert.AreEqual("a//b", res.String);
		}

		[Test]
		public void FloorDiv_InsideACommentIsIgnored()
		{
			AssertNumber(3, @"
				-- 1 // 0 is not evaluated
				--[[ neither // is this ]]
				return 7 // 2;");
		}

		[Test]
		public void Vanilla_DivisionStillParses()
		{
			AssertNumber(2.5, "return 10 / 4;");
		}

		[Test]
		public void Vanilla_DivideByNegativeStillParses()
		{
			AssertNumber(-5, "return 10 / -2;");
		}

		// ---------------------------------------------------------------
		// It emits a new opcode, so make sure a dumped chunk still round-trips
		// ---------------------------------------------------------------

		[Test]
		public void FloorDiv_SurvivesBinaryDumpRoundTrip()
		{
			string script = @"
				local x = 100
				x //= 7
				return (x // 2) .. '|' .. (-7 // 2);";

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
				Assert.AreEqual("7|-4", res.String);
			}
		}

		// ---------------------------------------------------------------
		// Rejected shapes
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void FloorDivAssign_WithMultipleTargets_IsSyntaxError()
		{
			Run("local a, b = 1, 2; a, b //= 1");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void FloorDivAssign_AsAnExpression_IsSyntaxError()
		{
			Run("local a, b = 1, 1; b = (a //= 1)");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void FloorDivAssign_InLocalDeclaration_IsSyntaxError()
		{
			Run("local x //= 1");
		}
	}
}
