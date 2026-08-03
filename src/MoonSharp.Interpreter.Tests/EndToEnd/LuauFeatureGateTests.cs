using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
	/// <summary>
	/// The Luau syntax extensions are opt-in through ScriptOptions.LuauFeatures. These tests cover
	/// the gate itself; the behaviour of each extension when enabled lives in its own fixture.
	/// </summary>
	[TestFixture]
	public class LuauFeatureGateTests
	{
		private static Script NewScript(LuauFeatures features)
		{
			Script s = new Script();
			s.Options.LuauFeatures = features;
			return s;
		}

		// ---------------------------------------------------------------
		// Defaults
		// ---------------------------------------------------------------

		[Test]
		public void Default_IsNone()
		{
			Assert.AreEqual(LuauFeatures.None, new Script().Options.LuauFeatures);
		}

		[Test]
		public void DefaultOptions_ArePropagatedToNewScripts()
		{
			LuauFeatures saved = Script.DefaultOptions.LuauFeatures;

			try
			{
				Script.DefaultOptions.LuauFeatures = LuauFeatures.All;
				Assert.AreEqual(LuauFeatures.All, new Script().Options.LuauFeatures);
			}
			finally
			{
				Script.DefaultOptions.LuauFeatures = saved;
			}
		}

		// ---------------------------------------------------------------
		// Compound assignment is off by default
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void CompoundAdd_IsSyntaxErrorWhenDisabled()
		{
			NewScript(LuauFeatures.None).DoString("local x = 1; x += 1; return x;");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void CompoundConcat_IsSyntaxErrorWhenDisabled()
		{
			NewScript(LuauFeatures.None).DoString("local s = 'a'; s ..= 'b'; return s;");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void CompoundSub_IsSyntaxErrorWhenDisabled()
		{
			NewScript(LuauFeatures.None).DoString("local x = 1; x -= 1; return x;");
		}

		[Test]
		public void CompoundAdd_WorksWhenEnabled()
		{
			DynValue res = NewScript(LuauFeatures.CompoundAssignment).DoString("local x = 1; x += 1; return x;");
			Assert.AreEqual(2, res.Number);
		}

		// ---------------------------------------------------------------
		// 'continue' is off by default, in which case it is only an identifier
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Continue_IsSyntaxErrorWhenDisabled()
		{
			NewScript(LuauFeatures.None).DoString("for i = 1, 3 do continue end");
		}

		[Test]
		public void Continue_IsStillAnIdentifierWhenDisabled()
		{
			DynValue res = NewScript(LuauFeatures.None).DoString("local continue = 7; return continue + 1;");
			Assert.AreEqual(8, res.Number);
		}

		[Test]
		public void Continue_WorksWhenEnabled()
		{
			DynValue res = NewScript(LuauFeatures.ContinueStatement).DoString(@"
				local s = 0
				for i = 1, 4 do
					if i == 2 then continue end
					s = s + i
				end
				return s;");
			Assert.AreEqual(8, res.Number);
		}

		// ---------------------------------------------------------------
		// The if-then-else expression is off by default, in which case 'if'
		// only ever starts a statement
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void IfExpression_IsSyntaxErrorWhenDisabled()
		{
			NewScript(LuauFeatures.None).DoString("return if true then 1 else 2;");
		}

		[Test]
		public void IfExpression_WorksWhenEnabled()
		{
			DynValue res = NewScript(LuauFeatures.IfExpression).DoString("return if true then 1 else 2;");
			Assert.AreEqual(1, res.Number);
		}

		[Test]
		public void Disabled_IfStatementStillParses()
		{
			DynValue res = NewScript(LuauFeatures.None).DoString(@"
				local x = 0
				if false then x = 1 elseif true then x = 3 else x = 2 end
				return x;");
			Assert.AreEqual(3, res.Number);
		}

		// ---------------------------------------------------------------
		// Floor division is off by default, in which case '//' is two
		// separate division operators
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void FloorDiv_IsSyntaxErrorWhenDisabled()
		{
			NewScript(LuauFeatures.None).DoString("return 7 // 2;");
		}

		[Test]
		public void FloorDiv_WorksWhenEnabled()
		{
			DynValue res = NewScript(LuauFeatures.FloorDivision).DoString("return 7 // 2;");
			Assert.AreEqual(3, res.Number);
		}

		[Test]
		public void Disabled_DivisionStillParses()
		{
			DynValue res = NewScript(LuauFeatures.None).DoString("return 10 / 4;");
			Assert.AreEqual(2.5, res.Number);
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void FloorDivAssign_NeedsCompoundAssignmentToo()
		{
			// '//=' is a compound assignment as much as it is a floor division
			NewScript(LuauFeatures.FloorDivision).DoString("local x = 7; x //= 2; return x;");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void FloorDivAssign_NeedsFloorDivisionToo()
		{
			NewScript(LuauFeatures.CompoundAssignment).DoString("local x = 7; x //= 2; return x;");
		}

		[Test]
		public void FloorDivAssign_WorksWithBothEnabled()
		{
			DynValue res = NewScript(LuauFeatures.FloorDivision | LuauFeatures.CompoundAssignment)
				.DoString("local x = 7; x //= 2; return x;");
			Assert.AreEqual(3, res.Number);
		}

		// ---------------------------------------------------------------
		// The number literal extensions are off by default, in which case
		// '0b1010' is 0 followed by a name and '1_000' is 1 followed by one
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void BinaryLiteral_IsSyntaxErrorWhenDisabled()
		{
			NewScript(LuauFeatures.None).DoString("local x = 0b1010; return x;");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void DigitSeparator_IsSyntaxErrorWhenDisabled()
		{
			NewScript(LuauFeatures.None).DoString("local x = 1_000; return x;");
		}

		[Test]
		public void NumberLiterals_WorkWhenEnabled()
		{
			DynValue res = NewScript(LuauFeatures.NumberLiterals).DoString("return 0b1010 + 1_000;");
			Assert.AreEqual(1010, res.Number);
		}

		[Test]
		public void Disabled_HexAndExponentsStillParse()
		{
			DynValue res = NewScript(LuauFeatures.None).DoString("return 0x1F + 1e3 + .5 + 0x10p4;");
			Assert.AreEqual(31 + 1000 + 0.5 + 256, res.Number);
		}

		// ---------------------------------------------------------------
		// The flags are independent
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void ContinueEnabled_DoesNotEnableCompoundAssignment()
		{
			NewScript(LuauFeatures.ContinueStatement).DoString("local x = 1; x += 1; return x;");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void CompoundAssignmentEnabled_DoesNotEnableContinue()
		{
			NewScript(LuauFeatures.CompoundAssignment).DoString("for i = 1, 3 do continue end");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void IfExpressionEnabled_DoesNotEnableCompoundAssignment()
		{
			NewScript(LuauFeatures.IfExpression).DoString("local x = 1; x += 1; return x;");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void ContinueEnabled_DoesNotEnableIfExpression()
		{
			NewScript(LuauFeatures.ContinueStatement).DoString("return if true then 1 else 2;");
		}

		// ---------------------------------------------------------------
		// With the extensions off, the affected operators lex exactly as before
		// ---------------------------------------------------------------

		[Test]
		public void Disabled_UnaryMinusAfterOperatorsStillParses()
		{
			DynValue res = NewScript(LuauFeatures.None).DoString("return 3 * -2 + 10 / -5 - -1;");
			Assert.AreEqual(-7, res.Number);
		}

		[Test]
		public void Disabled_VarargsAndConcatStillParse()
		{
			DynValue res = NewScript(LuauFeatures.None).DoString(@"
				local function f(...) return 'a' .. select('#', ...) end
				return f(1, 2);");
			Assert.AreEqual("a2", res.String);
		}

		[Test]
		public void Disabled_ComparisonOperatorsStillParse()
		{
			DynValue res = NewScript(LuauFeatures.None).DoString("return 1 <= 2 and 2 >= 1 and 1 ~= 2 and 1 == 1;");
			Assert.AreEqual(true, res.Boolean);
		}
	}
}
