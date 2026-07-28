using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
	/// <summary>
	/// Tests for the Luau-style compound assignment operators:
	/// += -= *= /= %= ^= ..=
	/// See https://rfcs.luau.org/syntax-compound-assignment.html
	/// </summary>
	[TestFixture]
	public class CompoundAssignmentTests
	{
		private static DynValue Run(string script)
		{
			return Script.RunString(script);
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
		// One test per operator, on a local
		// ---------------------------------------------------------------

		[Test]
		public void CompoundAdd_Local()
		{
			AssertNumber(7, "local x = 3; x += 4; return x;");
		}

		[Test]
		public void CompoundSub_Local()
		{
			AssertNumber(6, "local x = 10; x -= 4; return x;");
		}

		[Test]
		public void CompoundMul_Local()
		{
			AssertNumber(30, "local x = 5; x *= 6; return x;");
		}

		[Test]
		public void CompoundDiv_Local()
		{
			AssertNumber(4, "local x = 20; x /= 5; return x;");
		}

		[Test]
		public void CompoundMod_Local()
		{
			AssertNumber(2, "local x = 17; x %= 5; return x;");
		}

		[Test]
		public void CompoundPwr_Local()
		{
			AssertNumber(8, "local x = 2; x ^= 3; return x;");
		}

		[Test]
		public void CompoundConcat_Local()
		{
			AssertString("hello world", "local x = 'hello'; x ..= ' world'; return x;");
		}

		// ---------------------------------------------------------------
		// Other kinds of l-value
		// ---------------------------------------------------------------

		[Test]
		public void CompoundAdd_Global()
		{
			AssertNumber(11, "x = 4; x += 7; return x;");
		}

		[Test]
		public void CompoundAdd_Upvalue()
		{
			AssertNumber(15, @"
				local x = 5
				local function bump() x += 10 end
				bump()
				return x;");
		}

		[Test]
		public void CompoundAdd_TableField()
		{
			AssertNumber(9, "local t = { a = 4 }; t.a += 5; return t.a;");
		}

		[Test]
		public void CompoundAdd_TableIndex()
		{
			AssertNumber(9, "local t = { 4 }; t[1] += 5; return t[1];");
		}

		[Test]
		public void CompoundConcat_TableField()
		{
			AssertString("ab", "local t = { s = 'a' }; t.s ..= 'b'; return t.s;");
		}

		[Test]
		public void CompoundAdd_NestedTableField()
		{
			AssertNumber(3, "local t = { inner = { cost = 1 } }; t.inner.cost += 2; return t.inner.cost;");
		}

		// ---------------------------------------------------------------
		// The RFC's key requirement: l-value subexpressions are evaluated ONCE
		// ---------------------------------------------------------------

		[Test]
		public void CompoundAssignment_EvaluatesKeyExpressionOnce()
		{
			AssertNumber(1, @"
				local t = { 10 }
				local calls = 0
				local function key()
					calls = calls + 1
					return 1
				end
				t[key()] += 5
				return calls;");
		}

		[Test]
		public void CompoundAssignment_EvaluatesKeyExpressionOnce_StillAssigns()
		{
			AssertNumber(15, @"
				local t = { 10 }
				local function key() return 1 end
				t[key()] += 5
				return t[1];");
		}

		[Test]
		public void CompoundAssignment_EvaluatesBaseExpressionOnce()
		{
			AssertNumber(1, @"
				local t = { cost = 1 }
				local calls = 0
				local function get()
					calls = calls + 1
					return t
				end
				get().cost += 2
				return calls;");
		}

		[Test]
		public void CompoundAssignment_EvaluatesBaseExpressionOnce_StillAssigns()
		{
			AssertNumber(3, @"
				local t = { cost = 1 }
				local function get() return t end
				get().cost += 2
				return t.cost;");
		}

		// ---------------------------------------------------------------
		// The right-hand side is a full expression
		// ---------------------------------------------------------------

		[Test]
		public void CompoundSub_RightHandSideIsWholeExpression()
		{
			// x = x - (2 + 3), not (x - 2) + 3
			AssertNumber(5, "local x = 10; x -= 2 + 3; return x;");
		}

		[Test]
		public void CompoundPwr_RightHandSideIsWholeExpression()
		{
			// x = 2 ^ (3 ^ 2) = 2 ^ 9
			AssertNumber(512, "local x = 2; x ^= 3 ^ 2; return x;");
		}

		[Test]
		public void CompoundConcat_RightHandSideIsWholeExpression()
		{
			AssertString("abc", "local x = 'a'; x ..= 'b' .. 'c'; return x;");
		}

		[Test]
		public void CompoundAdd_RightHandSideCanBeACall()
		{
			AssertNumber(12, @"
				local function five() return 5 end
				local x = 7
				x += five()
				return x;");
		}

		// ---------------------------------------------------------------
		// Metamethods: no new ones, the existing ones fire (once each)
		// ---------------------------------------------------------------

		[Test]
		public void CompoundAdd_UsesAddMetamethod()
		{
			AssertNumber(30, @"
				local mt = { __add = function(a, b) return setmetatable({ v = a.v + b.v }, getmetatable(a)) end }
				local x = setmetatable({ v = 10 }, mt)
				local y = setmetatable({ v = 20 }, mt)
				x += y
				return x.v;");
		}

		[Test]
		public void CompoundAdd_UsesIndexAndNewIndexMetamethods()
		{
			AssertNumber(7, @"
				local store = { hp = 4 }
				local reads, writes = 0, 0
				local proxy = setmetatable({}, {
					__index = function(t, k) reads = reads + 1; return store[k] end,
					__newindex = function(t, k, v) writes = writes + 1; store[k] = v end,
				})
				proxy.hp += 3
				return store.hp;");
		}

		[Test]
		public void CompoundAdd_IndexAndNewIndexMetamethodsFireOnceEach()
		{
			AssertNumber(11, @"
				local store = { hp = 4 }
				local reads, writes = 0, 0
				local proxy = setmetatable({}, {
					__index = function(t, k) reads = reads + 1; return store[k] end,
					__newindex = function(t, k, v) writes = writes + 1; store[k] = v end,
				})
				proxy.hp += 3
				return reads * 10 + writes;");
		}

		[Test]
		public void CompoundAdd_UsesNewIndexMetamethod_WithComputedKey()
		{
			// A computed key keeps base+key on the stack across the read-modify-write, and a
			// __newindex *function* unwinds the stack differently from a plain table store.
			// This exercises both at once.
			AssertNumber(7, @"
				local store = { hp = 4 }
				local function key() return 'hp' end
				local proxy = setmetatable({}, {
					__index = function(t, k) return store[k] end,
					__newindex = function(t, k, v) store[k] = v end,
				})
				proxy[key()] += 3
				return store.hp;");
		}

		[Test]
		public void CompoundConcat_UsesConcatMetamethod()
		{
			AssertString("!", @"
				local mt = { __concat = function(a, b) return '!' end }
				local x = setmetatable({}, mt)
				x ..= 'ignored'
				return x;");
		}

		// ---------------------------------------------------------------
		// The statement leaves the stack balanced
		// ---------------------------------------------------------------

		[Test]
		public void CompoundAssignment_InALoop()
		{
			AssertNumber(55, @"
				local sum = 0
				for i = 1, 10 do
					sum += i
				end
				return sum;");
		}

		[Test]
		public void CompoundAssignment_OnTableInALoop()
		{
			AssertNumber(55, @"
				local t = { total = 0 }
				for i = 1, 10 do
					t.total += i
				end
				return t.total;");
		}

		[Test]
		public void CompoundAssignment_OnTableIndexInALoop()
		{
			AssertNumber(55, @"
				local t = { 0 }
				for i = 1, 10 do
					t[1] += i
				end
				return t[1];");
		}

		// ---------------------------------------------------------------
		// Vanilla Lua that must keep working (lexer regressions)
		// ---------------------------------------------------------------

		[Test]
		public void Vanilla_NegativeLiteralAfterAssignmentStillParses()
		{
			AssertNumber(-1, "local x =- 1; return x;");
		}

		[Test]
		public void Vanilla_CommentAfterExpressionStillParses()
		{
			AssertNumber(3, "local x = 1 + 2 --[[c]] ; return x;");
		}

		[Test]
		public void Vanilla_VarargsStillParses()
		{
			AssertNumber(3, @"
				local function f(...) return select('#', ...) end
				return f(1, 2, 3);");
		}

		[Test]
		public void Vanilla_ConcatFollowedByEqualityStillParses()
		{
			DynValue res = Run("return ('a' .. 'b') == 'ab';");
			Assert.AreEqual(DataType.Boolean, res.Type);
			Assert.AreEqual(true, res.Boolean);
		}

		// ---------------------------------------------------------------
		// Compound assignment emits Copy/Swap in shapes nothing else emits,
		// so make sure a dumped chunk still round-trips.
		// ---------------------------------------------------------------

		[Test]
		public void CompoundAssignment_SurvivesBinaryDumpRoundTrip()
		{
			string script = @"
				local t = { 1 }
				local function key() return 1 end
				t[key()] += 5
				t[1] *= 2
				local s = 'a'
				s ..= 'b'
				return t[1] .. s;";

			Script s1 = new Script();
			DynValue v1 = s1.LoadString(script);

			using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
			{
				s1.Dump(v1, ms);
				ms.Seek(0, System.IO.SeekOrigin.Begin);

				Script s2 = new Script();
				DynValue res = s2.LoadStream(ms).Function.Call();

				Assert.AreEqual(DataType.String, res.Type);
				Assert.AreEqual("12ab", res.String);
			}
		}

		// ---------------------------------------------------------------
		// Rejected shapes: compound assignment is a statement, not an expression
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void CompoundAssignment_InLocalDeclaration_IsSyntaxError()
		{
			Run("local x += 1");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void CompoundAssignment_WithMultipleTargets_IsSyntaxError()
		{
			Run("local a, b = 1, 2; a, b += 1");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void CompoundAssignment_WithMultipleValues_IsSyntaxError()
		{
			Run("local a = 1; a += 1, 2");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void CompoundAssignment_AsAnExpression_IsSyntaxError()
		{
			Run("local a, b = 1, 1; b = (a += 1)");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void CompoundAssignment_ToACall_IsSyntaxError()
		{
			Run("local function f() end; f() += 1");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void CompoundAssignment_ToALiteral_IsSyntaxError()
		{
			Run("1 += 1");
		}
	}
}
