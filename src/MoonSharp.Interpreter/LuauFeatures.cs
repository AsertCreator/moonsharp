using System;

namespace MoonSharp.Interpreter
{
	/// <summary>
	/// Syntax extensions borrowed from Luau (https://luau.org) which are not part of Lua 5.2.
	/// These are off by default; enable them through <see cref="ScriptOptions.LuauFeatures"/>.
	/// </summary>
	[Flags]
	public enum LuauFeatures
	{
		/// <summary>
		/// No Luau extensions - plain Lua 5.2 syntax.
		/// </summary>
		None = 0,

		/// <summary>
		/// The compound assignment operators '+=', '-=', '*=', '/=', '%=', '^=' and '..='.
		/// See https://rfcs.luau.org/syntax-compound-assignment.html
		/// </summary>
		CompoundAssignment = 0x1,

		/// <summary>
		/// The 'continue' statement, which jumps to the next iteration of the innermost loop.
		/// 'continue' stays a valid identifier - it is a context sensitive keyword, not a reserved one.
		/// See https://rfcs.luau.org/syntax-continue-statement.html
		/// </summary>
		ContinueStatement = 0x2,

		/// <summary>
		/// Interpolated strings, delimited by backticks, with expressions in braces:
		/// `hello {name}`. See https://rfcs.luau.org/syntax-string-interpolation.html
		/// </summary>
		StringInterpolation = 0x4,

		/// <summary>
		/// The 'if ... then ... else ...' expression, which yields a value instead of running a
		/// block: 'local s = if x &lt; 0 then -1 else 1'. The else branch is mandatory.
		/// See https://rfcs.luau.org/syntax-if-expression.html
		/// </summary>
		IfExpression = 0x8,

		/// <summary>
		/// The floor division operator '//', which is 'math.floor(a / b)', and its compound form
		/// '//='. The compound form additionally needs <see cref="CompoundAssignment"/>.
		/// See https://rfcs.luau.org/syntax-floor-division-operator.html
		/// </summary>
		FloorDivision = 0x10,

		/// <summary>
		/// Binary number literals ('0b1010') and digit separators ('1_000_000', '0xDE_AD_BE_EF').
		/// A separator is purely visual and may sit anywhere inside a number, as in Luau.
		/// See https://luau.org/syntax
		/// </summary>
		NumberLiterals = 0x20,

		/// <summary>
		/// Type annotations: 'local x: number', parameter and return types, generic parameters,
		/// 'type' and 'export type' aliases, and the 'expr :: T' assertion.
		///
		/// MoonSharp has no type checker, so every annotation is parsed for syntax and then
		/// discarded. Nothing here affects what a script does at run time; it exists so that
		/// annotated Luau source loads instead of failing to parse.
		///
		/// Note this makes '::' ambiguous with a goto label, which Luau does not have because it
		/// dropped goto. The label wins: '::name::' is always a label, so a chained assertion
		/// has to be parenthesised as '(x :: any) :: number' while this is enabled. Ordinary Lua
		/// keeps working either way, which a script that never asked for any of this relies on.
		/// See https://luau.org/typecheck
		/// </summary>
		TypeAnnotations = 0x40,

		/// <summary>
		/// All the Luau extensions supported by this version of MoonSharp. Note that this is not
		/// version stable - scripts relying on a specific set of extensions should name them.
		/// </summary>
		All = CompoundAssignment | ContinueStatement | StringInterpolation | IfExpression | FloorDivision | NumberLiterals | TypeAnnotations,
	}
}
