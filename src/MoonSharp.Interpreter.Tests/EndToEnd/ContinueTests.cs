using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
	/// <summary>
	/// Tests for the Luau-style 'continue' statement.
	/// See https://rfcs.luau.org/syntax-continue-statement.html
	/// </summary>
	[TestFixture]
	public class ContinueTests
	{
		private static Script NewScript()
		{
			Script s = new Script();
			s.Options.LuauFeatures = LuauFeatures.ContinueStatement;
			return s;
		}

		private static void AssertNumber(double expected, string script)
		{
			DynValue res = NewScript().DoString(script);
			Assert.AreEqual(DataType.Number, res.Type);
			Assert.AreEqual(expected, res.Number);
		}

		// ---------------------------------------------------------------
		// One test per loop kind
		// ---------------------------------------------------------------

		[Test]
		public void Continue_NumericFor_SkipsRestOfBody()
		{
			AssertNumber(25, @"
				local s = 0
				for i = 1, 10 do
					if i % 2 == 0 then continue end
					s = s + i
				end
				return s;");
		}

		[Test]
		public void Continue_While_SkipsRestOfBody()
		{
			AssertNumber(25, @"
				local i, s = 0, 0
				while i < 10 do
					i = i + 1
					if i % 2 == 0 then continue end
					s = s + i
				end
				return s;");
		}

		[Test]
		public void Continue_Repeat_SkipsToUntil()
		{
			AssertNumber(25, @"
				local i, s = 0, 0
				repeat
					i = i + 1
					if i % 2 == 0 then continue end
					s = s + i
				until i >= 10
				return s;");
		}

		[Test]
		public void Continue_GenericFor_SkipsRestOfBody()
		{
			AssertNumber(9, @"
				local s = 0
				for _, v in ipairs({ 1, 2, 3, 4, 5 }) do
					if v % 2 == 0 then continue end
					s = s + v
				end
				return s;");
		}

		// ---------------------------------------------------------------
		// Nesting
		// ---------------------------------------------------------------

		[Test]
		public void Continue_AffectsInnermostLoopOnly()
		{
			// inner loop contributes 2 per outer iteration, outer adds 100 each time
			AssertNumber(306, @"
				local s = 0
				for i = 1, 3 do
					for j = 1, 3 do
						if j == 2 then continue end
						s = s + 1
					end
					s = s + 100
				end
				return s;");
		}

		[Test]
		public void Continue_FromInsideNestedBlocks()
		{
			AssertNumber(3, @"
				local s = 0
				for i = 1, 3 do
					do
						local a = i
						if a > 0 then
							s = s + 1
							continue
						end
					end
					s = s + 100
				end
				return s;");
		}

		[Test]
		public void Continue_InnerBlockLocalsDoNotLeakAcrossIterations()
		{
			AssertNumber(3, @"
				local s = 0
				for i = 1, 3 do
					do
						local a
						if a == nil then s = s + 1 end
						local b = 5
						continue
					end
				end
				return s;");
		}

		[Test]
		public void Continue_InWhileWithNoOwnLocals_ButNestedBlockLocals()
		{
			// 'while' and 'repeat' declare no locals of their own, so their scope block's To stays
			// at -1 and the clean range emitted for continue has to be anchored off From instead.
			AssertNumber(0, @"
				local s, i = 0, 0
				while i < 3 do
					i = i + 1
					do
						local a = 1
						if a then continue end
					end
					s = s + 100
				end
				return s;");
		}

		[Test]
		public void Continue_InRepeatWithNoOwnLocals_ButNestedBlockLocals()
		{
			AssertNumber(0, @"
				local s, i = 0, 0
				repeat
					i = i + 1
					do
						local a = 1
						if a then continue end
					end
					s = s + 100
				until i >= 3
				return s;");
		}

		// ---------------------------------------------------------------
		// 'continue' is a context-sensitive keyword, not a reserved word
		// ---------------------------------------------------------------

		[Test]
		public void Continue_AsLocalVariableName()
		{
			AssertNumber(5, "local continue = 5 return continue;");
		}

		[Test]
		public void Continue_AsAssignmentTarget()
		{
			AssertNumber(5, "continue = 5 return continue;");
		}

		[Test]
		public void Continue_AsCallTarget()
		{
			AssertNumber(5, @"
				local r = 0
				continue = function(n) r = n end
				continue(5)
				return r;");
		}

		[Test]
		public void Continue_AsMultiAssignmentTarget()
		{
			AssertNumber(3, "continue, x = 1, 2 return continue + x;");
		}

		[Test]
		public void Continue_AsIndexedTarget()
		{
			AssertNumber(7, "continue = {} continue.x = 7 return continue.x;");
		}

		[Test]
		public void Continue_AsMethodCallTarget()
		{
			AssertNumber(4, @"
				local r = 0
				continue = { m = function(self, n) r = n end }
				continue:m(4)
				return r;");
		}

		// ---------------------------------------------------------------
		// Rejected shapes
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Continue_OutsideALoop_IsSyntaxError()
		{
			NewScript().DoString("continue");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Continue_InsideFunctionInsideLoop_IsSyntaxError()
		{
			NewScript().DoString("for i = 1, 3 do local f = function() continue end end");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Continue_FollowedByAnotherStatement_IsSyntaxError()
		{
			// continue terminates its block, like break and return
			NewScript().DoString("for i = 1, 3 do continue local x = 1 end");
		}

		// ---------------------------------------------------------------
		// repeat/until: the RFC forbids until reading a local declared
		// after the continue
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Continue_InRepeat_WhenUntilReadsLaterLocal_IsSyntaxError()
		{
			NewScript().DoString(@"
				local i = 0
				repeat
					i = i + 1
					if i == 1 then continue end
					local y = 1
				until y == 1");
		}

		[Test]
		public void Continue_InRepeat_WhenUntilReadsEarlierLocal_IsAllowed()
		{
			AssertNumber(3, @"
				local i = 0
				repeat
					i = i + 1
					local x = i
					if i < 3 then continue end
				until x >= 3
				return i;");
		}

		[Test]
		public void Continue_InRepeat_WhenLaterLocalIsNotReadByUntil_IsAllowed()
		{
			// the RFC only forbids until *reading* the later local
			AssertNumber(3, @"
				local i = 0
				repeat
					i = i + 1
					if i < 3 then continue end
					local y = 1
				until i >= 3
				return i;");
		}
	}
}
