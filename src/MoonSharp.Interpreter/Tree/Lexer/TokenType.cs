
namespace MoonSharp.Interpreter.Tree
{
	enum TokenType
	{
		Eof,
		HashBang,
		Name,
		And,
		Break,
		Do,
		Else,
		ElseIf,
		End,
		False,
		For,
		Function,
		Lambda,
		Goto,
		If,
		In,
		Local,
		Nil,
		Not,
		Or,
		Repeat,
		Return,
		Then,
		True,
		Until,
		While,
		Op_Equal,
		Op_Assignment,
		Op_LessThan,
		Op_LessThanEqual,
		Op_GreaterThanEqual,
		Op_GreaterThan,
		Op_NotEqual,
		Op_Concat,
		VarArgs,
		Dot,
		Colon,
		DoubleColon,
		Comma,
		Brk_Close_Curly,
		Brk_Open_Curly,
		Brk_Close_Round,
		Brk_Open_Round,
		Brk_Close_Square,
		Brk_Open_Square,
		Op_Len,
		Op_Pwr,
		Op_Mod,
		Op_Div,
		Op_Mul,
		Op_MinusOrSub,
		Op_Add,
		Comment,

		String,
		String_Long,

		Number,
		Number_HexFloat,
		Number_Hex,
		SemiColon,
		Invalid,

		Brk_Open_Curly_Shared,
		Op_Dollar,

		// Compound assignment operators - https://rfcs.luau.org/syntax-compound-assignment.html
		Op_AddAssign,
		Op_SubAssign,
		Op_MulAssign,
		Op_DivAssign,
		Op_ModAssign,
		Op_PwrAssign,
		Op_ConcatAssign,

		// Interpolated strings - https://rfcs.luau.org/syntax-string-interpolation.html
		// A string with holes lexes as InterpBegin (expr InterpMid)* expr InterpEnd, each token
		// carrying the literal text next to it. One with no holes lexes as a plain String.
		// One with no holes lexes as String_Interp rather than String, so that the RFC's ban on
		// using an interpolated string as a bare call argument holds either way.
		String_Interp,
		String_InterpBegin,
		String_InterpMid,
		String_InterpEnd,

		// Floor division - https://rfcs.luau.org/syntax-floor-division-operator.html
		Op_FloorDiv,
		Op_FloorDivAssign,

		// Binary literals - https://luau.org/syntax
		Number_Binary,
	}



}
