using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
	/// <summary>
	/// Tests for Luau-style interpolated strings.
	/// See https://rfcs.luau.org/syntax-string-interpolation.html
	/// </summary>
	[TestFixture]
	public class StringInterpolationTests
	{
		private static Script NewScript()
		{
			Script s = new Script();
			s.Options.LuauFeatures = LuauFeatures.StringInterpolation;
			return s;
		}

		private static void AssertString(string expected, string script)
		{
			DynValue res = NewScript().DoString(script);
			Assert.AreEqual(DataType.String, res.Type);
			Assert.AreEqual(expected, res.String);
		}

		// ---------------------------------------------------------------
		// Substitution
		// ---------------------------------------------------------------

		[Test]
		public void Interp_SingleHole_SubstitutesValue()
		{
			AssertString("hello world", @"
				local name = 'world'
				return `hello {name}`;");
		}

		[Test]
		public void Interp_MultipleHoles_SubstitutesInOrder()
		{
			AssertString("a-1-b-2-c", @"
				local x, y = 1, 2
				return `a-{x}-b-{y}-c`;");
		}

		[Test]
		public void Interp_AdjacentHoles_HaveNoSeparator()
		{
			AssertString("12", "local x, y = 1, 2 return `{x}{y}`;");
		}

		[Test]
		public void Interp_NoHoles_IsAnOrdinaryString()
		{
			AssertString("hello", "return `hello`;");
		}

		[Test]
		public void Interp_EmptyString_IsEmpty()
		{
			AssertString("", "return ``;");
		}

		[Test]
		public void Interp_HoleHoldsAnArbitraryExpression()
		{
			AssertString("sum is 7", "return `sum is {3 + 4}`;");
		}

		[Test]
		public void Interp_HoleHoldsAFunctionCall()
		{
			AssertString("len 5", @"
				local function f(s) return #s end
				return `len {f('abcde')}`;");
		}

		[Test]
		public void Interp_HoleMayContainATableConstructor()
		{
			// the '}' closing the table must not be read as closing the hole
			AssertString("3", "return `{#{10, 20, 30}}`;");
		}

		[Test]
		public void Interp_HoleMayContainANestedInterpolatedString()
		{
			AssertString("outer inner 1 end", @"
				local x = 1
				return `outer {`inner {x}`} end`;");
		}

		[Test]
		public void Interp_HoleMayContainNewlines()
		{
			// the RFC allows newlines inside the braces, though not in the literal text
			AssertString("v=3", @"return `v={
				1 + 2
			}`;");
		}

		// ---------------------------------------------------------------
		// tostring semantics for holes
		// ---------------------------------------------------------------

		[Test]
		public void Interp_BooleanHole_UsesToStringSemantics()
		{
			AssertString("it is true", "return `it is {true}`;");
		}

		[Test]
		public void Interp_NilHole_RendersAsNil()
		{
			AssertString("got nil", "return `got {nil}`;");
		}

		[Test]
		public void Interp_HoleHonoursToStringMetamethod()
		{
			AssertString("value: custom", @"
				local t = setmetatable({}, { __tostring = function() return 'custom' end })
				return `value: {t}`;");
		}

		// ---------------------------------------------------------------
		// Interaction with the rest of the language
		// ---------------------------------------------------------------

		[Test]
		public void Interp_WorksAsAParenthesisedCallArgument()
		{
			AssertString("hi 1", @"
				local function id(s) return s end
				local x = 1
				return id(`hi {x}`);");
		}

		[Test]
		public void Interp_ComposesWithTheConcatOperator()
		{
			AssertString("ab", "return `a` .. `b`;");
		}

		[Test]
		public void Interp_WorksInsideATableConstructor()
		{
			AssertString("v1", "local x = 1 local t = { `v{x}` } return t[1];");
		}

		[Test]
		public void Interp_HoleCanYieldAcrossACoroutine()
		{
			// ToStr can call __tostring, so it is a yield point and the half built string has to
			// survive being suspended
			AssertString("a1b", @"
				local co = coroutine.create(function()
					return `a{coroutine.yield()}b`
				end)
				coroutine.resume(co)
				local ok, res = coroutine.resume(co, 1)
				return res;");
		}

		[Test]
		public void Interp_ToStringMetamethodCanYield()
		{
			AssertString("v=7", @"
				local t = setmetatable({}, { __tostring = function() return coroutine.yield() end })
				local co = coroutine.create(function() return `v={t}` end)
				coroutine.resume(co)
				local ok, res = coroutine.resume(co, 7)
				return res;");
		}

		// ---------------------------------------------------------------
		// Escapes
		// ---------------------------------------------------------------

		[Test]
		public void Interp_EscapedOpenBrace_IsALiteralBrace()
		{
			AssertString("a{b", @"return `a\{b`;");
		}

		[Test]
		public void Interp_CloseBrace_NeedsNoEscape()
		{
			AssertString("a}b", "return `a}b`;");
		}

		[Test]
		public void Interp_EscapedBacktick_IsALiteralBacktick()
		{
			AssertString("a`b", @"return `a\`b`;");
		}

		[Test]
		public void Interp_StandardEscapesStillWork()
		{
			AssertString("a\tb\nc", @"return `a\tb\nc`;");
		}

		[Test]
		public void Interp_UnicodeEscape_IsNotReadAsAHole()
		{
			// the braces of \u{...} belong to the escape, not to a hole
			AssertString("HI", @"return `\u{48}\u{49}`;");
		}

		[Test]
		public void Interp_EscapedNewline_ContinuesTheString()
		{
			AssertString("a\nb", "return `a\\\nb`;");
		}

		// ---------------------------------------------------------------
		// Syntax errors
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Interp_RawNewlineInText_IsSyntaxError()
		{
			NewScript().DoString("return `a\nb`;");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Interp_Unterminated_IsSyntaxError()
		{
			NewScript().DoString("return `hello");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Interp_UnterminatedHole_IsSyntaxError()
		{
			NewScript().DoString("return `hello {x");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Interp_DoubleOpenBrace_IsSyntaxError()
		{
			// the RFC rejects '{{' outright, as people coming from C# or Python will expect it
			// to be an escape for a literal brace
			NewScript().DoString("return `{{1, 2, 3}}`;");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Interp_EmptyHole_IsSyntaxError()
		{
			NewScript().DoString("return `a{}b`;");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Interp_AsBareCallArgument_IsSyntaxError()
		{
			// the RFC prohibits print`x` without parentheses
			NewScript().DoString("print`hello`");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Interp_AsBareCallArgumentWithHole_IsSyntaxError()
		{
			NewScript().DoString("local x = 1 print`hello {x}`");
		}

		// ---------------------------------------------------------------
		// Feature gate
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Interp_IsSyntaxErrorWhenDisabled()
		{
			new Script().DoString("return `hello`;");
		}

		[Test]
		public void Interp_BacktickIsStillInvalidElsewhereWhenDisabled()
		{
			// guards against the backtick becoming valid outside the feature gate
			Assert.Throws<SyntaxErrorException>(() => new Script().DoString("local x = 1 return x `y`"));
		}
	}
}
