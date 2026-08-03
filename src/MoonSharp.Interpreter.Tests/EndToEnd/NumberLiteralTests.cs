using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
	/// <summary>
	/// Tests for the Luau number literal extensions: binary literals ('0b1010') and digit
	/// separators ('1_000_000'). See https://luau.org/syntax
	/// </summary>
	[TestFixture]
	public class NumberLiteralTests
	{
		private static Script NewScript()
		{
			Script s = new Script();
			s.Options.LuauFeatures = LuauFeatures.NumberLiterals;
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
		// Binary literals
		// ---------------------------------------------------------------

		[Test]
		public void Binary_Simple()
		{
			AssertNumber(10, "return 0b1010;");
		}

		[Test]
		public void Binary_UppercasePrefix()
		{
			AssertNumber(10, "return 0B1010;");
		}

		[Test]
		public void Binary_Zero()
		{
			AssertNumber(0, "return 0b0;");
		}

		[Test]
		public void Binary_One()
		{
			AssertNumber(1, "return 0b1;");
		}

		[Test]
		public void Binary_Byte()
		{
			AssertNumber(255, "return 0b11111111;");
		}

		[Test]
		public void Binary_LeadingZeroesAreFree()
		{
			// 80 leading zeroes, so the literal is far longer than 64 chars but still means 1
			AssertNumber(1, "return 0b" + new string('0', 80) + "1;");
		}

		[Test]
		public void Binary_FullWord()
		{
			AssertNumber(4294967295, "return 0b" + new string('1', 32) + ";");
		}

		[Test]
		public void Binary_SixtyFourBitsIsTheLimit()
		{
			// 2^63 is a 1 followed by 63 zeroes, which is exactly 64 significant bits
			DynValue res = Run("return 0b1" + new string('0', 63) + " == 2 ^ 63;");
			Assert.AreEqual(true, res.Boolean);
		}

		[Test]
		public void Binary_InArithmetic()
		{
			AssertNumber(11, "return 0b1010 + 1;");
		}

		[Test]
		public void Binary_UnaryMinus()
		{
			AssertNumber(-10, "return -0b1010;");
		}

		[Test]
		public void Binary_AsATableKey()
		{
			AssertNumber(7, "local t = {}; t[0b10] = 7; return t[2];");
		}

		[Test]
		public void Binary_WithBit32()
		{
			AssertNumber(8, "return bit32.band(0b1100, 0b1010);");
		}

		// ---------------------------------------------------------------
		// Digit separators
		// ---------------------------------------------------------------

		[Test]
		public void Separator_InADecimalInteger()
		{
			AssertNumber(1000000, "return 1_000_000;");
		}

		[Test]
		public void Separator_InAFloat()
		{
			AssertNumber(3.141592, "return 3.141_592;");
		}

		[Test]
		public void Separator_InAnExponent()
		{
			AssertNumber(1e10, "return 1e1_0;");
		}

		[Test]
		public void Separator_InAHexLiteral()
		{
			AssertNumber(57005, "return 0xDE_AD;");
		}

		[Test]
		public void Separator_InAHexFloat()
		{
			AssertNumber(256, "return 0x1_0p4;");
		}

		[Test]
		public void Separator_InABinaryLiteral()
		{
			AssertNumber(170, "return 0b1010_1010;");
		}

		[Test]
		public void Separator_RunsAreAllowed()
		{
			// Luau's lexer does not police placement, so neither does this one
			AssertNumber(10, "return 1__0;");
		}

		[Test]
		public void Separator_TrailingIsAllowed()
		{
			AssertNumber(1, "return 1_;");
		}

		[Test]
		public void Separator_DoesNotMakeALeadingUnderscoreANumber()
		{
			AssertNumber(5, "local _1 = 5; return _1;");
		}

		[Test]
		public void Separator_NamesWithUnderscoresStillLex()
		{
			AssertNumber(3, "local my_var_1 = 3; return my_var_1;");
		}

		// ---------------------------------------------------------------
		// Malformed binary literals
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Binary_WithNoDigits_IsSyntaxError()
		{
			Run("return 0b;");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Binary_WithADecimalDigit_IsSyntaxError()
		{
			Run("return 0b102;");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Binary_WithAFractionalPart_IsSyntaxError()
		{
			Run("return 0b1.1;");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Binary_OverSixtyFourBits_IsSyntaxError()
		{
			// a 1 followed by 64 zeroes needs 65 bits
			Run("return 0b1" + new string('0', 64) + ";");
		}

		// ---------------------------------------------------------------
		// Vanilla number syntax is untouched
		// ---------------------------------------------------------------

		[Test]
		public void Vanilla_HexStillParses()
		{
			AssertNumber(31, "return 0x1F;");
		}

		[Test]
		public void Vanilla_HexFloatStillParses()
		{
			AssertNumber(256, "return 0x10p4;");
		}

		[Test]
		public void Vanilla_ExponentStillParses()
		{
			AssertNumber(1000, "return 1e3;");
		}

		[Test]
		public void Vanilla_NegativeExponentStillParses()
		{
			AssertNumber(0.001, "return 1e-3;");
		}

		[Test]
		public void Vanilla_LeadingDotStillParses()
		{
			AssertNumber(0.5, "return .5;");
		}

		[Test]
		public void Vanilla_LeadingZeroIsNotOctal()
		{
			AssertNumber(7, "return 07;");
		}

		[Test]
		public void Vanilla_ZeroStillParses()
		{
			AssertNumber(0, "return 0;");
		}
	}
}
