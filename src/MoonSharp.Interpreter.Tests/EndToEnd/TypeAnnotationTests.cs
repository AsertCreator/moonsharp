using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
	/// <summary>
	/// Tests for Luau type annotations. See https://luau.org/typecheck
	///
	/// MoonSharp has no type checker, so every one of these is really asking two things: that the
	/// annotated source parses, and that the annotation changed nothing about what it does.
	/// </summary>
	[TestFixture]
	public class TypeAnnotationTests
	{
		private static Script NewScript()
		{
			Script s = new Script();
			s.Options.LuauFeatures = LuauFeatures.TypeAnnotations;
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

		private static void AssertParses(string script)
		{
			// a bare 'load' proves it parsed without also running it
			NewScript().LoadString(script);
		}

		// ---------------------------------------------------------------
		// Local declarations
		// ---------------------------------------------------------------

		[Test]
		public void Local_SingleAnnotation()
		{
			AssertNumber(1, "local x: number = 1; return x;");
		}

		[Test]
		public void Local_MultipleAnnotations()
		{
			AssertNumber(3, "local a: number, b: number = 1, 2; return a + b;");
		}

		[Test]
		public void Local_MixedAnnotatedAndNot()
		{
			AssertNumber(3, "local a: number, b = 1, 2; return a + b;");
		}

		[Test]
		public void Local_AnnotationWithNoValue()
		{
			AssertNumber(1, "local x: number; x = 1; return x;");
		}

		[Test]
		public void Local_OptionalType()
		{
			AssertNumber(1, "local x: number? = 1; return x;");
		}

		[Test]
		public void Local_UnionType()
		{
			AssertNumber(1, "local x: number | string = 1; return x;");
		}

		[Test]
		public void Local_IntersectionType()
		{
			AssertParses("local x: Foo & Bar = nil");
		}

		[Test]
		public void Local_QualifiedType()
		{
			AssertParses("local x: Mod.Thing = nil");
		}

		[Test]
		public void Local_GenericType()
		{
			AssertParses("local x: Array<number> = nil");
		}

		[Test]
		public void Local_NestedGenericType()
		{
			AssertParses("local x: Map<string, Array<number>> = nil");
		}

		[Test]
		public void Local_TableTypeArrayShorthand()
		{
			AssertParses("local x: {number} = nil");
		}

		[Test]
		public void Local_TableTypeProperties()
		{
			AssertNumber(3, "local p: { x: number, y: number } = { x = 1, y = 2 }; return p.x + p.y;");
		}

		[Test]
		public void Local_TableTypeIndexer()
		{
			AssertParses("local t: { [string]: number } = {}");
		}

		[Test]
		public void Local_TableTypeMixed()
		{
			AssertParses("local t: { n: number, [string]: number } = {}");
		}

		[Test]
		public void Local_TableTypeSemicolonSeparated()
		{
			AssertParses("local t: { x: number; y: number } = {}");
		}

		[Test]
		public void Local_TableTypeTrailingSeparator()
		{
			AssertParses("local t: { x: number, y: number, } = {}");
		}

		[Test]
		public void Local_EmptyTableType()
		{
			AssertParses("local t: {} = {}");
		}

		[Test]
		public void Local_FunctionType()
		{
			AssertParses("local f: (number, string) -> boolean = nil");
		}

		[Test]
		public void Local_FunctionTypeNoArgsNoReturns()
		{
			AssertParses("local f: () -> () = nil");
		}

		[Test]
		public void Local_FunctionTypeNamedParams()
		{
			AssertParses("local f: (count: number, name: string) -> boolean = nil");
		}

		[Test]
		public void Local_FunctionTypeReturningFunction()
		{
			AssertParses("local f: (number) -> (number) -> number = nil");
		}

		[Test]
		public void Local_FunctionTypeMultipleReturns()
		{
			AssertParses("local f: () -> (number, string) = nil");
		}

		[Test]
		public void Local_FunctionTypeVariadicParam()
		{
			AssertParses("local f: (...number) -> () = nil");
		}

		[Test]
		public void Local_SingletonTypes()
		{
			AssertParses("local d: 'up' | 'down' | nil = nil");
		}

		[Test]
		public void Local_BooleanSingletonTypes()
		{
			AssertParses("local b: true | false = nil");
		}

		[Test]
		public void Local_TypeofType()
		{
			AssertParses("local a = 1 local b: typeof(a) = 2");
		}

		[Test]
		public void Local_ParenthesisedType()
		{
			AssertParses("local x: (number) = nil");
		}

		[Test]
		public void Local_LeadingUnionSeparator()
		{
			AssertParses(@"
				local x:
					| number
					| string = 1");
		}

		// ---------------------------------------------------------------
		// Functions
		// ---------------------------------------------------------------

		[Test]
		public void Function_ParameterAnnotations()
		{
			AssertNumber(3, @"
				local function add(a: number, b: number)
					return a + b
				end
				return add(1, 2);");
		}

		[Test]
		public void Function_ReturnAnnotation()
		{
			AssertNumber(3, @"
				local function add(a: number, b: number): number
					return a + b
				end
				return add(1, 2);");
		}

		[Test]
		public void Function_MultipleReturnAnnotation()
		{
			AssertNumber(3, @"
				local function two(): (number, number)
					return 1, 2
				end
				local a, b = two()
				return a + b;");
		}

		[Test]
		public void Function_NoReturnsAnnotation()
		{
			AssertNumber(1, @"
				local n = 0
				local function bump(): ()
					n = n + 1
				end
				bump()
				return n;");
		}

		[Test]
		public void Function_VarargsAnnotation()
		{
			AssertNumber(3, @"
				local function count(...: number)
					return select('#', ...)
				end
				return count(1, 2, 3);");
		}

		[Test]
		public void Function_AnonymousWithAnnotations()
		{
			AssertNumber(5, @"
				local f = function(a: number): number return a + 1 end
				return f(4);");
		}

		[Test]
		public void Function_GlobalWithAnnotations()
		{
			AssertNumber(3, @"
				function add(a: number, b: number): number
					return a + b
				end
				return add(1, 2);");
		}

		[Test]
		public void Function_FieldWithAnnotations()
		{
			AssertNumber(3, @"
				t = {}
				function t.add(a: number, b: number): number
					return a + b
				end
				return t.add(1, 2);");
		}

		[Test]
		public void Function_MethodWithAnnotations()
		{
			AssertNumber(3, @"
				t = { base = 1 }
				function t:add(b: number): number
					return self.base + b
				end
				return t:add(2);");
		}

		[Test]
		public void Function_GenericParameters()
		{
			AssertNumber(7, @"
				local function id<T>(v: T): T
					return v
				end
				return id(7);");
		}

		[Test]
		public void Function_MultipleGenericParameters()
		{
			AssertNumber(3, @"
				local function pair<K, V>(k: K, v: V): number
					return 3
				end
				return pair('a', 1);");
		}

		[Test]
		public void Function_GenericTypePack()
		{
			AssertParses(@"
				local function f<T, U...>(a: T): number
					return 1
				end");
		}

		[Test]
		public void Function_GlobalWithGenericParameters()
		{
			AssertNumber(7, @"
				function id<T>(v: T): T
					return v
				end
				return id(7);");
		}

		[Test]
		public void Function_FieldWithGenericParameters()
		{
			AssertNumber(7, @"
				t = {}
				function t.id<T>(v: T): T
					return v
				end
				return t.id(7);");
		}

		[Test]
		public void Function_TableTypeReturn()
		{
			AssertNumber(1, @"
				local function make(): { x: number }
					return { x = 1 }
				end
				return make().x;");
		}

		// ---------------------------------------------------------------
		// Loops
		// ---------------------------------------------------------------

		[Test]
		public void For_NumericLoopVariableAnnotation()
		{
			AssertNumber(6, @"
				local s = 0
				for i: number = 1, 3 do s = s + i end
				return s;");
		}

		[Test]
		public void For_GenericLoopVariableAnnotations()
		{
			AssertNumber(9, @"
				local s = 0
				for _: number, v: number in ipairs({ 2, 3, 4 }) do s = s + v end
				return s;");
		}

		[Test]
		public void For_GenericLoopFirstVariableAnnotatedOnly()
		{
			AssertNumber(9, @"
				local s = 0
				for _: number, v in ipairs({ 2, 3, 4 }) do s = s + v end
				return s;");
		}

		// ---------------------------------------------------------------
		// Type aliases
		// ---------------------------------------------------------------

		[Test]
		public void TypeAlias_Simple()
		{
			AssertNumber(1, "type Count = number local x: Count = 1 return x;");
		}

		[Test]
		public void TypeAlias_Table()
		{
			AssertNumber(3, @"
				type Point = { x: number, y: number }
				local p: Point = { x = 1, y = 2 }
				return p.x + p.y;");
		}

		[Test]
		public void TypeAlias_Generic()
		{
			AssertParses("type Box<T> = { value: T }");
		}

		[Test]
		public void TypeAlias_GenericWithDefault()
		{
			AssertParses("type Box<T = number> = { value: T }");
		}

		[Test]
		public void TypeAlias_Union()
		{
			AssertParses("type Result = number | string | nil");
		}

		[Test]
		public void TypeAlias_FunctionType()
		{
			AssertParses("type Handler = (event: string) -> ()");
		}

		[Test]
		public void TypeAlias_Exported()
		{
			AssertNumber(1, "export type Count = number local x: Count = 1 return x;");
		}

		[Test]
		public void TypeAlias_EmitsNoCode()
		{
			// the alias sits between two statements and must not disturb either
			AssertNumber(3, @"
				local a = 1
				type Ignored = number
				local b = 2
				return a + b;");
		}

		// ---------------------------------------------------------------
		// 'type' and 'export' stay ordinary identifiers
		// ---------------------------------------------------------------

		[Test]
		public void Type_IsStillCallableAsAFunction()
		{
			DynValue res = Run("return type(1);");
			Assert.AreEqual("number", res.String);
		}

		[Test]
		public void Type_IsStillAssignable()
		{
			AssertNumber(5, "type = 5 return type;");
		}

		[Test]
		public void Type_IsStillALocalName()
		{
			AssertNumber(5, "local type = 5 return type;");
		}

		[Test]
		public void Type_IsStillAFieldTarget()
		{
			AssertNumber(5, "type = {} type.x = 5 return type.x;");
		}

		[Test]
		public void Export_IsStillAnOrdinaryName()
		{
			AssertNumber(5, "export = 5 return export;");
		}

		// ---------------------------------------------------------------
		// Type assertions
		// ---------------------------------------------------------------

		[Test]
		public void Assertion_OnALocal()
		{
			AssertNumber(1, "local x = 1 return x :: number;");
		}

		[Test]
		public void Assertion_DoesNotChangeTheValue()
		{
			DynValue res = Run("return ('a' :: string) .. 'b';");
			Assert.AreEqual("ab", res.String);
		}

		[Test]
		public void Assertion_BindsTighterThanBinaryOperators()
		{
			// (1 :: number) + 2, not 1 :: (number + 2)
			AssertNumber(3, "return 1 :: number + 2;");
		}

		[Test]
		public void Assertion_OnACallResult()
		{
			AssertNumber(7, @"
				local function f() return 7 end
				return f() :: number;");
		}

		[Test]
		public void Assertion_ChainedNeedsParentheses()
		{
			// '::any::' on its own would be a goto label, so a chain has to be parenthesised
			AssertNumber(1, "return (1 :: any) :: number;");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Assertion_ChainedWithoutParentheses_IsSyntaxError()
		{
			// pinned deliberately: the label reading of '::any::' wins over the assertion one
			Run("return 1 :: any :: number;");
		}

		[Test]
		public void Assertion_ToATableType()
		{
			AssertNumber(1, "local t = { x = 1 } return (t :: { x: number }).x;");
		}

		[Test]
		public void Assertion_InAnArgument()
		{
			AssertNumber(1, @"
				local function id(v) return v end
				return id(1 :: number);");
		}

		// ---------------------------------------------------------------
		// Goto labels keep working, which is what '::' is disambiguated for
		// ---------------------------------------------------------------

		[Test]
		public void Label_AfterALocalDeclaration()
		{
			AssertNumber(3, @"
				local i = 0
				::top::
				i = i + 1
				if i < 3 then goto top end
				return i;");
		}

		[Test]
		public void Label_AfterAnAssignment()
		{
			AssertNumber(3, @"
				i = 0
				::top::
				i = i + 1
				if i < 3 then goto top end
				return i;");
		}

		[Test]
		public void Label_AfterACall()
		{
			AssertNumber(3, @"
				local i = 0
				local function bump() i = i + 1 end
				::top::
				bump()
				if i < 3 then goto top end
				return i;");
		}

		[Test]
		public void Label_AndAnAssertionInTheSameChunk()
		{
			AssertNumber(3, @"
				local i: number = 0
				::top::
				i = (i :: number) + 1
				if i < 3 then goto top end
				return i;");
		}

		// ---------------------------------------------------------------
		// Malformed annotations are still syntax errors
		// ---------------------------------------------------------------

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Malformed_MissingTypeAfterColon()
		{
			Run("local x: = 1");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Malformed_UnclosedGenericArgs()
		{
			Run("local x: Array<number = nil");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Malformed_UnclosedTableType()
		{
			Run("local x: { a: number = nil");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Malformed_DanglingUnion()
		{
			Run("local x: number | = 1");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Malformed_ArrowWithoutReturnType()
		{
			Run("local f: (number) -> = nil");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Malformed_TypeAliasWithoutValue()
		{
			Run("type Foo");
		}

		[Test]
		[ExpectedException(typeof(SyntaxErrorException))]
		public void Malformed_AssertionWithoutType()
		{
			Run("local x = 1 :: ");
		}
	}
}
