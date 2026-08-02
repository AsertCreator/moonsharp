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
		/// All the Luau extensions supported by this version of MoonSharp. Note that this is not
		/// version stable - scripts relying on a specific set of extensions should name them.
		/// </summary>
		All = CompoundAssignment | ContinueStatement | StringInterpolation,
	}
}
