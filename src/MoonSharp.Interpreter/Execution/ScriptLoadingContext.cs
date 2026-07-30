using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Tree;
using System.Collections.Generic;

namespace MoonSharp.Interpreter.Execution
{
	class ScriptLoadingContext
	{
		public Script Script { get; private set; }
		public BuildTimeScope Scope { get; set; }
		public SourceCode Source { get; set; }
		public bool Anonymous { get; set; }
		public bool IsDynamicExpression { get; set; }
		public Lexer Lexer { get; set; }

		private readonly List<ParseTimeLoop> m_ParseTimeLoops = new List<ParseTimeLoop>();

		public ScriptLoadingContext(Script s)
		{
			Script = s;
		}

		internal void PushParseTimeLoop(ParseTimeLoop loop)
		{
			m_ParseTimeLoops.Add(loop);
		}

		internal void PopParseTimeLoop()
		{
			m_ParseTimeLoops.RemoveAt(m_ParseTimeLoops.Count - 1);
		}

		/// <summary>
		/// The innermost loop currently being parsed, or null if there isn't one. Function bodies
		/// are a boundary, so a loop outside a nested function is not visible from inside it.
		/// </summary>
		internal ParseTimeLoop CurrentParseTimeLoop
		{
			get { return m_ParseTimeLoops.Count > 0 ? m_ParseTimeLoops[m_ParseTimeLoops.Count - 1] : null; }
		}

	}
}
