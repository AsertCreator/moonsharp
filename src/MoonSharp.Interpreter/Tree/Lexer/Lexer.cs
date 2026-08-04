using System.Collections.Generic;
using System.Text;

namespace MoonSharp.Interpreter.Tree
{
	class Lexer
	{
		Token m_Current = null;
		string m_Code;
		int m_PrevLineTo = 0;
		int m_PrevColTo = 1;
		int m_Cursor = 0;
		int m_Line = 1;
		int m_Col = 0;
		int m_SourceId;
		bool m_AutoSkipComments = false;
		LuauFeatures m_LuauFeatures = LuauFeatures.None;

		/// <summary>
		/// One entry per interpolated string currently being read, holding the number of '{' opened
		/// inside its current hole. A '}' seen with the top entry at zero closes the hole and puts us
		/// back into string text; any other '}' is an ordinary one, closing a table or a block.
		/// </summary>
		List<int> m_InterpolationBraces = new List<int>();

		public Lexer(int sourceID, string scriptContent, bool autoSkipComments, LuauFeatures luauFeatures = LuauFeatures.None)
		{
			m_Code = scriptContent;
			m_SourceId = sourceID;

			// remove unicode BOM if any
			if (m_Code.Length > 0 && m_Code[0] == 0xFEFF)
				m_Code = m_Code.Substring(1);

			m_AutoSkipComments = autoSkipComments;
			m_LuauFeatures = luauFeatures;
		}

		private bool CompoundAssignmentEnabled
		{
			get { return (m_LuauFeatures & LuauFeatures.CompoundAssignment) != 0; }
		}

		private bool StringInterpolationEnabled
		{
			get { return (m_LuauFeatures & LuauFeatures.StringInterpolation) != 0; }
		}

		private bool FloorDivisionEnabled
		{
			get { return (m_LuauFeatures & LuauFeatures.FloorDivision) != 0; }
		}

		private bool NumberLiteralsEnabled
		{
			get { return (m_LuauFeatures & LuauFeatures.NumberLiterals) != 0; }
		}

		private bool TypeAnnotationsEnabled
		{
			get { return (m_LuauFeatures & LuauFeatures.TypeAnnotations) != 0; }
		}

		/// <summary>
		/// True when the cursor is inside a hole of an interpolated string, so braces are tracked.
		/// </summary>
		private bool InInterpolationHole
		{
			get { return m_InterpolationBraces.Count > 0; }
		}

		public Token Current
		{
			get
			{
				if (m_Current == null)
					Next();

				return m_Current;
			}
		}

		private Token FetchNewToken()
		{
			while (true)
			{
				Token T = ReadToken();

				//System.Diagnostics.Debug.WriteLine("LEXER : " + T.ToString());

				if ((T.Type != TokenType.Comment && T.Type != TokenType.HashBang) || (!m_AutoSkipComments))
					return T;
			}
		}

		public void Next()
		{
			m_Current = FetchNewToken();
		}

		/// <summary>
		/// Whether the '::' the cursor is on opens a goto label ('::name::') rather than a Luau
		/// type assertion ('expr :: type'). The two are indistinguishable until three tokens in,
		/// which is one more than PeekNext offers, so this snapshots and restores the same way.
		///
		/// A chained assertion ('x :: any :: number') looks identical to a label from here and so
		/// loses, which is why it needs parentheses when type annotations are enabled. Labels win
		/// because they are ordinary Lua and may appear in scripts that never asked for any of
		/// this, whereas chained assertions only ever appear in source written against Luau.
		/// </summary>
		public bool IsAtGotoLabel()
		{
			int snapshot = m_Cursor;
			Token current = m_Current;
			int line = m_Line;
			int col = m_Col;
			List<int> interpolationBraces = new List<int>(m_InterpolationBraces);

			try
			{
				Next();

				if (Current.Type != TokenType.Name)
					return false;

				Next();

				return Current.Type == TokenType.DoubleColon;
			}
			catch (SyntaxErrorException)
			{
				// whatever is ahead does not lex, so it is not a label - let the real parse report it
				return false;
			}
			finally
			{
				m_Cursor = snapshot;
				m_Current = current;
				m_Line = line;
				m_Col = col;
				m_InterpolationBraces = interpolationBraces;
			}
		}

		public Token PeekNext()
		{
			int snapshot = m_Cursor;
			Token current = m_Current;
			int line = m_Line;
			int col = m_Col;
			List<int> interpolationBraces = new List<int>(m_InterpolationBraces);

			Next();
			Token t = Current;

			m_Cursor = snapshot;
			m_Current = current;
			m_Line = line;
			m_Col = col;
			m_InterpolationBraces = interpolationBraces;

			return t;
		}


		private void CursorNext()
		{
			if (CursorNotEof())
			{
				if (CursorChar() == '\n')
				{
					m_Col = 0;
					m_Line += 1;
				}
				else
				{
					m_Col += 1;
				}

				m_Cursor += 1;
			}
		}

		private char CursorChar()
		{
			if (m_Cursor < m_Code.Length)
				return m_Code[m_Cursor];
			else
				return '\0'; //  sentinel
		}

		private char CursorCharNext()
		{
			CursorNext();
			return CursorChar();
		}

		private bool CursorMatches(string pattern)
		{
			for (int i = 0; i < pattern.Length; i++)
			{
				int j = m_Cursor + i;

				if (j >= m_Code.Length)
					return false;
				if (m_Code[j] != pattern[i])
					return false;
			}
			return true;
		}

		private bool CursorNotEof()
		{
			return m_Cursor < m_Code.Length;
		}

		private bool IsWhiteSpace(char c)
		{
			return char.IsWhiteSpace(c);
		}

		private void SkipWhiteSpace()
		{
			for (; CursorNotEof() && IsWhiteSpace(CursorChar()); CursorNext())
			{
			}
		}


		private Token ReadToken()
		{
			SkipWhiteSpace();

			int fromLine = m_Line;
			int fromCol = m_Col;

			if (!CursorNotEof())
				return CreateToken(TokenType.Eof, fromLine, fromCol, "<eof>");

			char c = CursorChar();

			switch (c)
			{
				case '|':
					CursorCharNext();
					return CreateToken(TokenType.Lambda, fromLine, fromCol, "|");
				case ';':
					CursorCharNext();
					return CreateToken(TokenType.SemiColon, fromLine, fromCol, ";");
				case '=':
					return PotentiallyDoubleCharOperator('=', TokenType.Op_Assignment, TokenType.Op_Equal, fromLine, fromCol);
				case '<':
					return PotentiallyDoubleCharOperator('=', TokenType.Op_LessThan, TokenType.Op_LessThanEqual, fromLine, fromCol);
				case '>':
					return PotentiallyDoubleCharOperator('=', TokenType.Op_GreaterThan, TokenType.Op_GreaterThanEqual, fromLine, fromCol);
				case '~':
				case '!':
					if (CursorCharNext() != '=')
						throw new SyntaxErrorException(CreateToken(TokenType.Invalid, fromLine, fromCol), "unexpected symbol near '{0}'", c);

					CursorCharNext();
					return CreateToken(TokenType.Op_NotEqual, fromLine, fromCol, "~=");
				case '.':
					{
						char next = CursorCharNext();
						if (next == '.')
							return ReadDotDotToken(fromLine, fromCol);
						else if (LexerUtils.CharIsDigit(next))
							return ReadNumberToken(fromLine, fromCol, true);
						else
							return CreateToken(TokenType.Dot, fromLine, fromCol, ".");
					}
				case '+':
					return PotentiallyCompoundAssignOperator(TokenType.Op_Add, TokenType.Op_AddAssign, fromLine, fromCol);
				case '-':
					{
						char next = CursorCharNext();
						if (next == '-')
						{
							return ReadComment(fromLine, fromCol);
						}
						else if (next == '=' && CompoundAssignmentEnabled)
						{
							CursorCharNext();
							return CreateToken(TokenType.Op_SubAssign, fromLine, fromCol, "-=");
						}
						else if (next == '>' && TypeAnnotationsEnabled)
						{
							CursorCharNext();
							return CreateToken(TokenType.Arrow, fromLine, fromCol, "->");
						}
						else
						{
							return CreateToken(TokenType.Op_MinusOrSub, fromLine, fromCol, "-");
						}
					}
				case '*':
					return PotentiallyCompoundAssignOperator(TokenType.Op_Mul, TokenType.Op_MulAssign, fromLine, fromCol);
				case '/':
					return ReadSlashToken(fromLine, fromCol);
				case '%':
					return PotentiallyCompoundAssignOperator(TokenType.Op_Mod, TokenType.Op_ModAssign, fromLine, fromCol);
				case '^':
					return PotentiallyCompoundAssignOperator(TokenType.Op_Pwr, TokenType.Op_PwrAssign, fromLine, fromCol);
				case '$':
					{
						Token dollar = PotentiallyDoubleCharOperator('{', TokenType.Op_Dollar, TokenType.Brk_Open_Curly_Shared, fromLine, fromCol);

						if (dollar.Type == TokenType.Brk_Open_Curly_Shared && InInterpolationHole)
							m_InterpolationBraces[m_InterpolationBraces.Count - 1] += 1;

						return dollar;
					}
				case '#':
					if (m_Cursor == 0 && m_Code.Length > 1 && m_Code[1] == '!')
						return ReadHashBang(fromLine, fromCol);

					return CreateSingleCharToken(TokenType.Op_Len, fromLine, fromCol);
				case '[':
					{
						char next = CursorCharNext();
						if (next == '=' || next == '[')
						{
							string str = ReadLongString(fromLine, fromCol, null, "string");
							return CreateToken(TokenType.String_Long, fromLine, fromCol, str);
						}
						return CreateToken(TokenType.Brk_Open_Square, fromLine, fromCol, "[");
					}
				case ']':
					return CreateSingleCharToken(TokenType.Brk_Close_Square, fromLine, fromCol);
				case '(':
					return CreateSingleCharToken(TokenType.Brk_Open_Round, fromLine, fromCol);
				case ')':
					return CreateSingleCharToken(TokenType.Brk_Close_Round, fromLine, fromCol);
				case '{':
					if (InInterpolationHole)
						m_InterpolationBraces[m_InterpolationBraces.Count - 1] += 1;

					return CreateSingleCharToken(TokenType.Brk_Open_Curly, fromLine, fromCol);
				case '}':
					if (InInterpolationHole)
					{
						int top = m_InterpolationBraces.Count - 1;

						if (m_InterpolationBraces[top] == 0)
						{
							CursorCharNext(); // skip the '}' which closes the hole
							return ReadInterpolatedStringPart(fromLine, fromCol, false);
						}

						m_InterpolationBraces[top] -= 1;
					}

					return CreateSingleCharToken(TokenType.Brk_Close_Curly, fromLine, fromCol);
				case '`':
					if (!StringInterpolationEnabled)
						throw new SyntaxErrorException(CreateToken(TokenType.Invalid, fromLine, fromCol), "unexpected symbol near '{0}'", c);

					CursorCharNext(); // skip the opening backtick
					m_InterpolationBraces.Add(0);
					return ReadInterpolatedStringPart(fromLine, fromCol, true);
				case '?':
					if (!TypeAnnotationsEnabled)
						throw new SyntaxErrorException(CreateToken(TokenType.Invalid, fromLine, fromCol), "unexpected symbol near '{0}'", c);

					return CreateSingleCharToken(TokenType.Op_Question, fromLine, fromCol);
				case '&':
					if (!TypeAnnotationsEnabled)
						throw new SyntaxErrorException(CreateToken(TokenType.Invalid, fromLine, fromCol), "unexpected symbol near '{0}'", c);

					return CreateSingleCharToken(TokenType.Op_Ampersand, fromLine, fromCol);
				case ',':
					return CreateSingleCharToken(TokenType.Comma, fromLine, fromCol);
				case ':':
					return PotentiallyDoubleCharOperator(':', TokenType.Colon, TokenType.DoubleColon, fromLine, fromCol);
				case '"':
				case '\'':
					return ReadSimpleStringToken(fromLine, fromCol);
				case '\0':
					throw new SyntaxErrorException(CreateToken(TokenType.Invalid, fromLine, fromCol), "unexpected symbol near '{0}'", CursorChar())
					{
						IsPrematureStreamTermination = true
					};
				default:
					{
						if (char.IsLetter(c) || c == '_')
						{
							string name = ReadNameToken();
							return CreateNameToken(name, fromLine, fromCol);
						}
						else if (LexerUtils.CharIsDigit(c))
						{
							return ReadNumberToken(fromLine, fromCol, false);
						}
					}

					throw new SyntaxErrorException(CreateToken(TokenType.Invalid, fromLine, fromCol), "unexpected symbol near '{0}'", CursorChar());
			}
		}

		private string ReadLongString(int fromLine, int fromCol, string startpattern, string subtypeforerrors)
		{
			// here we are at the first '=' or second '['
			StringBuilder text = new StringBuilder(1024);
			string end_pattern = "]";

			if (startpattern == null)
			{
				for (char c = CursorChar(); ; c = CursorCharNext())
				{
					if (c == '\0' || !CursorNotEof())
					{
						throw new SyntaxErrorException(
							CreateToken(TokenType.Invalid, fromLine, fromCol),
							"unfinished long {0} near '<eof>'", subtypeforerrors) { IsPrematureStreamTermination = true };
					}
					else if (c == '=')
					{
						end_pattern += "=";
					}
					else if (c == '[')
					{
						end_pattern += "]";
						break;
					}
					else
					{
						throw new SyntaxErrorException(
							CreateToken(TokenType.Invalid, fromLine, fromCol),
							"invalid long {0} delimiter near '{1}'", subtypeforerrors, c) { IsPrematureStreamTermination = true };
					}
				}
			}
			else
			{
				end_pattern = startpattern.Replace('[', ']');
			}


			for (char c = CursorCharNext(); ; c = CursorCharNext())
			{
				if (c == '\r') // XXI century and we still debate on how a newline is made. throw new DeveloperExtremelyAngryException.
					continue;

				if (c == '\0' || !CursorNotEof())
				{
					throw new SyntaxErrorException(
							CreateToken(TokenType.Invalid, fromLine, fromCol),
							"unfinished long {0} near '{1}'", subtypeforerrors, text.ToString()) { IsPrematureStreamTermination = true };
				}
				else if (c == ']' && CursorMatches(end_pattern))
				{
					for (int i = 0; i < end_pattern.Length; i++)
						CursorCharNext();

					return LexerUtils.AdjustLuaLongString(text.ToString());
				}
				else
				{
					text.Append(c);
				}
			}
		}

		private Token ReadNumberToken(int fromLine, int fromCol, bool leadingDot)
		{
			StringBuilder text = new StringBuilder(32);

			//INT : Digit+
			//HEX : '0' [xX] HexDigit+
			//FLOAT : Digit+ '.' Digit* ExponentPart?
			//		| '.' Digit+ ExponentPart?
			//		| Digit+ ExponentPart
			//HEX_FLOAT : '0' [xX] HexDigit+ '.' HexDigit* HexExponentPart?
			//			| '0' [xX] '.' HexDigit+ HexExponentPart?
			//			| '0' [xX] HexDigit+ HexExponentPart
			//
			// ExponentPart : [eE] [+-]? Digit+
			// HexExponentPart : [pP] [+-]? Digit+
			//
			// With LuauFeatures.NumberLiterals there is also
			//BINARY : '0' [bB] BinDigit+
			// and a '_' may appear anywhere inside any of the above, where it means nothing.

			bool isHex = false;
			bool isBinary = false;
			bool dotAdded = false;
			bool exponentPart = false;
			bool exponentSignAllowed = false;

			if (leadingDot)
			{
				text.Append("0.");
			}
			else if (CursorChar() == '0')
			{
				text.Append(CursorChar());
				char secondChar = CursorCharNext();

				if (secondChar == 'x' || secondChar == 'X')
				{
					isHex = true;
					text.Append(CursorChar());
					CursorCharNext();
				}
				else if ((secondChar == 'b' || secondChar == 'B') && NumberLiteralsEnabled)
				{
					isBinary = true;
					text.Append(CursorChar());
					CursorCharNext();
				}
			}

			for (char c = CursorChar(); CursorNotEof(); c = CursorCharNext())
			{
				if (exponentSignAllowed && (c == '+' || c == '-'))
				{
					exponentSignAllowed = false;
					text.Append(c);
				}
				else if (LexerUtils.CharIsDigit(c))
				{
					text.Append(c);
				}
				else if (c == '.' && !dotAdded)
				{
					dotAdded = true;
					text.Append(c);
				}
				else if (LexerUtils.CharIsHexDigit(c) && isHex && !exponentPart)
				{
					text.Append(c);
				}
				else if (c == 'e' || c == 'E' || (isHex && (c == 'p' || c == 'P')))
				{
					text.Append(c);
					exponentPart = true;
					exponentSignAllowed = true;
					dotAdded = true;
				}
				else if (c == '_' && NumberLiteralsEnabled)
				{
					// a separator carries no meaning, so it is dropped here rather than being
					// carried into the token text for the number parsers to deal with
				}
				else
				{
					break;
				}
			}

			TokenType numberType = TokenType.Number;

			if (isBinary)
				numberType = TokenType.Number_Binary;
			else if (isHex && (dotAdded || exponentPart))
				numberType = TokenType.Number_HexFloat;
			else if (isHex)
				numberType = TokenType.Number_Hex;

			string tokenStr = text.ToString();
			return CreateToken(numberType, fromLine, fromCol, tokenStr);
		}

		private Token CreateSingleCharToken(TokenType tokenType, int fromLine, int fromCol)
		{
			char c = CursorChar();
			CursorCharNext();
			return CreateToken(tokenType, fromLine, fromCol, c.ToString());
		}

		private Token ReadHashBang(int fromLine, int fromCol)
		{
			StringBuilder text = new StringBuilder(32);

			for (char c = CursorChar(); CursorNotEof(); c = CursorCharNext())
			{
				if (c == '\n')
				{
					CursorCharNext();
					return CreateToken(TokenType.HashBang, fromLine, fromCol, text.ToString());
				}
				else if (c != '\r')
				{
					text.Append(c);
				}
			}

			return CreateToken(TokenType.HashBang, fromLine, fromCol, text.ToString());
		}


		private Token ReadComment(int fromLine, int fromCol)
		{
			StringBuilder text = new StringBuilder(32);

			bool extraneousFound = false;

			for (char c = CursorCharNext(); CursorNotEof(); c = CursorCharNext())
			{
				if (c == '[' && !extraneousFound && text.Length > 0)
				{
					text.Append('[');
					//CursorCharNext();
					string comment = ReadLongString(fromLine, fromCol, text.ToString(), "comment");
					return CreateToken(TokenType.Comment, fromLine, fromCol, comment);
				}
				else if (c == '\n')
				{
					extraneousFound = true;
					CursorCharNext();
					return CreateToken(TokenType.Comment, fromLine, fromCol, text.ToString());
				}
				else if (c != '\r')
				{
					if (c != '[' && c != '=')
						extraneousFound = true;

					text.Append(c);
				}
			}

			return CreateToken(TokenType.Comment, fromLine, fromCol, text.ToString());
		}

		private Token ReadSimpleStringToken(int fromLine, int fromCol)
		{
			StringBuilder text = new StringBuilder(32);
			char separator = CursorChar();

			for (char c = CursorCharNext(); CursorNotEof(); c = CursorCharNext())
			{
			redo_Loop:

				if (c == '\\')
				{
					text.Append(c);
					c = CursorCharNext();
					text.Append(c);

					if (c == '\r')
					{
						c = CursorCharNext();
						if (c == '\n')
							text.Append(c);
						else
							goto redo_Loop;
					}
					else if (c == 'z')
					{
						c = CursorCharNext();

						if (char.IsWhiteSpace(c))
							SkipWhiteSpace();

						c = CursorChar();

						goto redo_Loop;
					}
				}
				else if (c == '\n' || c == '\r')
				{
					throw new SyntaxErrorException(
						CreateToken(TokenType.Invalid, fromLine, fromCol),
						"unfinished string near '{0}'", text.ToString());
				}
				else if (c == separator)
				{
					CursorCharNext();
					Token t = CreateToken(TokenType.String, fromLine, fromCol);
					t.Text = LexerUtils.UnescapeLuaString(t, text.ToString());
					return t;
				}
				else
				{
					text.Append(c);
				}
			}

			throw new SyntaxErrorException(
				CreateToken(TokenType.Invalid, fromLine, fromCol),
				"unfinished string near '{0}'", text.ToString()) { IsPrematureStreamTermination = true };
		}


		/// <summary>
		/// Reads one run of literal text of an interpolated string, starting just past the opening
		/// backtick (isFirst) or just past the '}' which closed the previous hole, and stopping at
		/// the '{' which opens the next hole or at the closing backtick.
		/// See https://rfcs.luau.org/syntax-string-interpolation.html
		/// </summary>
		private Token ReadInterpolatedStringPart(int fromLine, int fromCol, bool isFirst)
		{
			StringBuilder text = new StringBuilder(32);

			while (true)
			{
				char c = CursorChar();

				if (c == '\0' || !CursorNotEof())
				{
					throw new SyntaxErrorException(
						CreateToken(TokenType.Invalid, fromLine, fromCol),
						"unfinished string near '{0}'", text.ToString()) { IsPrematureStreamTermination = true };
				}
				else if (c == '\\')
				{
					ReadInterpolatedStringEscape(text);
				}
				else if (c == '\n' || c == '\r')
				{
					throw new SyntaxErrorException(
						CreateToken(TokenType.Invalid, fromLine, fromCol),
						"unfinished string near '{0}'", text.ToString());
				}
				else if (c == '{')
				{
					CursorCharNext(); // skip the '{' which opens the hole

					// the RFC rejects '{{' outright, since anyone arriving from C#, Rust or Python
					// reads it as an escape for a literal brace, which here it is not
					if (CursorChar() == '{')
					{
						throw new SyntaxErrorException(
							CreateToken(TokenType.Invalid, fromLine, fromCol),
							"unexpected '{{' in interpolated string, use '\\{' for a literal brace");
					}

					return CreateInterpolatedStringToken(isFirst ? TokenType.String_InterpBegin : TokenType.String_InterpMid,
						fromLine, fromCol, text.ToString());
				}
				else if (c == '`')
				{
					CursorCharNext(); // skip the closing backtick
					m_InterpolationBraces.RemoveAt(m_InterpolationBraces.Count - 1);

					// a string which never opened a hole still gets its own token type, so that the
					// RFC's ban on 'print`x`' does not depend on whether the string has holes
					return CreateInterpolatedStringToken(isFirst ? TokenType.String_Interp : TokenType.String_InterpEnd,
						fromLine, fromCol, text.ToString());
				}
				else
				{
					text.Append(c);
					CursorCharNext();
				}
			}
		}

		private Token CreateInterpolatedStringToken(TokenType tokenType, int fromLine, int fromCol, string text)
		{
			Token t = CreateToken(tokenType, fromLine, fromCol);
			t.Text = LexerUtils.UnescapeLuaString(t, text);
			return t;
		}

		/// <summary>
		/// Consumes one escape sequence of an interpolated string. '\{' and '\`' are specific to
		/// interpolated strings and are resolved here; everything else is kept verbatim for
		/// UnescapeLuaString, including '\u{...}', whose braces must not be read as a hole.
		/// </summary>
		private void ReadInterpolatedStringEscape(StringBuilder text)
		{
			char c = CursorCharNext(); // the character after the backslash

			if (c == '{' || c == '`')
			{
				text.Append(c);
				CursorCharNext();
				return;
			}

			text.Append('\\');
			text.Append(c);

			if (c == 'u')
			{
				for (c = CursorCharNext(); CursorNotEof() && c != '}'; c = CursorCharNext())
					text.Append(c);

				if (CursorNotEof())
					text.Append(c); // the '}' closing the code point
			}

			CursorCharNext();
		}


		private Token PotentiallyDoubleCharOperator(char expectedSecondChar, TokenType singleCharToken, TokenType doubleCharToken, int fromLine, int fromCol)
		{
			string op = CursorChar().ToString();

			CursorCharNext();

			if (CursorChar() == expectedSecondChar)
			{
				CursorCharNext();
				return CreateToken(doubleCharToken, fromLine, fromCol, op + expectedSecondChar);
			}
			else
				return CreateToken(singleCharToken, fromLine, fromCol, op);
		}


		/// <summary>
		/// Reads a single char operator which, if LuauFeatures.CompoundAssignment is enabled, may
		/// instead be the two char compound assignment form ('*' vs '*=').
		/// </summary>
		private Token PotentiallyCompoundAssignOperator(TokenType singleCharToken, TokenType compoundAssignToken, int fromLine, int fromCol)
		{
			if (!CompoundAssignmentEnabled)
				return CreateSingleCharToken(singleCharToken, fromLine, fromCol);

			return PotentiallyDoubleCharOperator('=', singleCharToken, compoundAssignToken, fromLine, fromCol);
		}


		/// <summary>
		/// Disambiguates the tokens starting with '/' : '/', '/=', '//' and '//='.
		/// Called with the cursor on the slash.
		///
		/// '//=' needs LuauFeatures.CompoundAssignment as well as LuauFeatures.FloorDivision,
		/// since it is both. With floor division off the two slashes lex as two separate '/'
		/// tokens, exactly as they did before the operator existed.
		/// </summary>
		private Token ReadSlashToken(int fromLine, int fromCol)
		{
			char next = CursorCharNext();

			if (next == '/' && FloorDivisionEnabled)
			{
				// this consumes the second slash whichever branch is taken, which is what '//' wants too
				if (CursorCharNext() == '=' && CompoundAssignmentEnabled)
				{
					CursorCharNext();
					return CreateToken(TokenType.Op_FloorDivAssign, fromLine, fromCol, "//=");
				}

				return CreateToken(TokenType.Op_FloorDiv, fromLine, fromCol, "//");
			}
			else if (next == '=' && CompoundAssignmentEnabled)
			{
				CursorCharNext();
				return CreateToken(TokenType.Op_DivAssign, fromLine, fromCol, "/=");
			}
			else
			{
				return CreateToken(TokenType.Op_Div, fromLine, fromCol, "/");
			}
		}


		/// <summary>
		/// Disambiguates the tokens starting with '..' : '..', '...' and '..='.
		/// Called with the cursor on the second dot.
		/// </summary>
		private Token ReadDotDotToken(int fromLine, int fromCol)
		{
			char next = CursorCharNext();

			if (next == '.')
			{
				CursorCharNext();
				return CreateToken(TokenType.VarArgs, fromLine, fromCol, "...");
			}
			else if (next == '=' && CompoundAssignmentEnabled)
			{
				CursorCharNext();
				return CreateToken(TokenType.Op_ConcatAssign, fromLine, fromCol, "..=");
			}
			else
			{
				return CreateToken(TokenType.Op_Concat, fromLine, fromCol, "..");
			}
		}



		private Token CreateNameToken(string name, int fromLine, int fromCol)
		{
			TokenType? reservedType = Token.GetReservedTokenType(name);

			if (reservedType.HasValue)
			{
				return CreateToken(reservedType.Value, fromLine, fromCol, name);
			}
			else
			{
				return CreateToken(TokenType.Name, fromLine, fromCol, name);
			}
		}


		private Token CreateToken(TokenType tokenType, int fromLine, int fromCol, string text = null)
		{
			Token t = new Token(tokenType, m_SourceId, fromLine, fromCol, m_Line, m_Col, m_PrevLineTo, m_PrevColTo)
			{
				Text = text
			};
			m_PrevLineTo = m_Line;
			m_PrevColTo = m_Col;
			return t;
		}

		private string ReadNameToken()
		{
			StringBuilder name = new StringBuilder(32);

			for (char c = CursorChar(); CursorNotEof(); c = CursorCharNext())
			{
				if (char.IsLetterOrDigit(c) || c == '_')
					name.Append(c);
				else
					break;
			}

			return name.ToString();
		}




	}
}
