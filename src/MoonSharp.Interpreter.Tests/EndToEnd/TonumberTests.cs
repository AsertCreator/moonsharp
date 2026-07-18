using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
	/// <summary>
	/// Fork tests for tonumber(e, base). The upstream implementation delegated to
	/// Convert.ToUInt32, which let raw CLR FormatException/OverflowException escape the
	/// interpreter on unparseable or oversized input instead of returning nil per Lua
	/// semantics — a host-crash primitive when tonumber is fed untrusted strings.
	/// </summary>
	[TestFixture]
	public class TonumberTests
	{
		private static DynValue Run(string code) { return Script.RunString(code); }
		private static double Num(string code) { return Run(code).Number; }
		private static bool IsNil(string code) { return Run(code).IsNil(); }

		[Test]
		public void Tonumber_WithBase_NullBackedStringReturnsNilNotCrash()
		{
			// A host can hand the interpreter a string DynValue wrapping a null
			// (DynValue.NewString(null)). Feeding it to tonumber(x, base) must yield nil,
			// not a raw NullReferenceException escaping into the host.
			Script s = new Script();
			s.Globals.Set("x", DynValue.NewString((string)null));
			DynValue r = s.DoString("return tonumber(x, 16)");
			Assert.IsTrue(r.IsNil());
		}

		[Test]
		public void Tonumber_WithBase_ParsesValidNumerals()
		{
			Assert.AreEqual(255.0, Num("return tonumber('ff', 16)"));
			Assert.AreEqual(255.0, Num("return tonumber('FF', 16)"));
			Assert.AreEqual(2.0, Num("return tonumber('10', 2)"));
			Assert.AreEqual(511.0, Num("return tonumber('777', 8)"));
			Assert.AreEqual(123.0, Num("return tonumber('123', 10)"));
			Assert.AreEqual(15.0, Num("return tonumber('120', 3)"));
			Assert.AreEqual(71.0, Num("return tonumber('78', 9)"));
		}

		[Test]
		public void Tonumber_WithBase_SupportsBasesUpTo36()
		{
			Assert.AreEqual(35.0, Num("return tonumber('z', 36)"));
			Assert.AreEqual(1295.0, Num("return tonumber('zz', 36)"));
			Assert.AreEqual(19.0, Num("return tonumber('j', 20)"));
		}

		[Test]
		public void Tonumber_WithBase_HandlesSignAndWhitespace()
		{
			Assert.AreEqual(-255.0, Num("return tonumber('-ff', 16)"));
			Assert.AreEqual(16.0, Num("return tonumber('  10  ', 16)"));
			Assert.AreEqual(-5.0, Num("return tonumber(' -101 ', 2)"));
		}

		[Test]
		public void Tonumber_WithBase_CoercesNumberFirstArgument()
		{
			Assert.AreEqual(5.0, Num("return tonumber(101, 2)"));
		}

		[Test]
		public void Tonumber_WithBase_ReturnsNilOnUnparseableInput()
		{
			// These previously escaped as a raw .NET FormatException.
			Assert.IsTrue(IsNil("return tonumber('zz', 16)"));
			Assert.IsTrue(IsNil("return tonumber('not hex!', 16)"));
			Assert.IsTrue(IsNil("return tonumber('0x10', 16)")); // '0x' prefix is not a hex digit in Lua
			Assert.IsTrue(IsNil("return tonumber('12.5', 10)"));
			Assert.IsTrue(IsNil("return tonumber('1 2', 16)"));
			Assert.IsTrue(IsNil("return tonumber('', 16)"));
			Assert.IsTrue(IsNil("return tonumber('   ', 16)"));
			Assert.IsTrue(IsNil("return tonumber('-', 16)"));
			Assert.IsTrue(IsNil("return tonumber('+10', 16)")); // only '-' is a valid sign in Lua
		}

		[Test]
		public void Tonumber_WithBase_ReturnsNilOnDigitOutOfBase()
		{
			// Previously raised ScriptRuntimeException 'invalid character' for bases 3-9.
			Assert.IsTrue(IsNil("return tonumber('2', 2)"));
			Assert.IsTrue(IsNil("return tonumber('9', 8)"));
			Assert.IsTrue(IsNil("return tonumber('17', 6)"));
			Assert.IsTrue(IsNil("return tonumber('ff', 10)"));
		}

		[Test]
		public void Tonumber_WithBase_LargeValuesDoNotThrow()
		{
			// Previously escaped as a raw .NET OverflowException (> uint.MaxValue).
			DynValue v = Run("return tonumber(string.rep('f', 20), 16)");
			Assert.AreEqual(DataType.Number, v.Type);
			Assert.Greater(v.Number, 1e23);
		}

		[Test]
		public void Tonumber_WithBase_OutOfRangeBaseStillRaises()
		{
			Assert.Throws<ScriptRuntimeException>(() => Run("return tonumber('10', 1)"));
			Assert.Throws<ScriptRuntimeException>(() => Run("return tonumber('10', 37)"));
			Assert.Throws<ScriptRuntimeException>(() => Run("return tonumber('10', 200)"));
		}

		[Test]
		public void Tonumber_WithBase_BadInputReturnsNilNotCrash()
		{
			// A script must be able to branch on bad input (nil) rather than have the
			// call escape as a CLR exception into the embedding host.
			DynValue v = Run(@"
				local ok = tonumber('deadbeef', 16)
				local bad = tonumber('DEAD BEEF QUOTE', 16)
				return ok ~= nil and bad == nil
			");
			Assert.IsTrue(v.Boolean);
		}
	}
}
