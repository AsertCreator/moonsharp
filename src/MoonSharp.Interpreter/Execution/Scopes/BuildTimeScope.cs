using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter.Execution.Scopes;
using MoonSharp.Interpreter.Tree.Statements;

namespace MoonSharp.Interpreter.Execution
{
	internal class BuildTimeScope
	{
		List<BuildTimeScopeFrame> m_Frames = new List<BuildTimeScopeFrame>();
		List<IClosureBuilder> m_ClosureBuilders = new List<IClosureBuilder>();
		List<SymbolRef> m_RecordedSymbols;


		public void PushFunction(IClosureBuilder closureBuilder, bool hasVarArgs)
		{
			m_ClosureBuilders.Add(closureBuilder);
			m_Frames.Add(new BuildTimeScopeFrame(hasVarArgs));
		}

		public void PushBlock()
		{
			m_Frames.Last().PushBlock();
		}

		public RuntimeScopeBlock PopBlock()
		{
			return m_Frames.Last().PopBlock();
		}

		public RuntimeScopeFrame PopFunction()
		{
			var last = m_Frames.Last();
			last.ResolveLRefs();
			m_Frames.RemoveAt(m_Frames.Count - 1);

			m_ClosureBuilders.RemoveAt(m_ClosureBuilders.Count - 1);

			return last.GetRuntimeFrameData();
		}


		internal Scopes.BuildTimeScopeBlock CurrentBlock
		{
			get { return m_Frames.Last().CurrentBlock; }
		}

		/// <summary>
		/// Starts collecting every symbol resolved by Find, so a caller can tell which locals an
		/// expression reads. Used by 'repeat' to enforce the continue rule on its until condition.
		/// </summary>
		internal void BeginSymbolRecording()
		{
			m_RecordedSymbols = new List<SymbolRef>();
		}

		internal List<SymbolRef> EndSymbolRecording()
		{
			var recorded = m_RecordedSymbols;
			m_RecordedSymbols = null;
			return recorded ?? new List<SymbolRef>();
		}

		public SymbolRef Find(string name)
		{
			SymbolRef local = m_Frames.Last().Find(name);

			if (local != null)
			{
				if (m_RecordedSymbols != null)
					m_RecordedSymbols.Add(local);

				return local;
			}

			for (int i = m_Frames.Count - 2; i >= 0; i--)
			{
				SymbolRef symb = m_Frames[i].Find(name);

				if (symb != null)
				{
					symb = CreateUpValue(this, symb, i, m_Frames.Count - 2);
						
					if (symb != null)
						return symb;
				}
			}

			return CreateGlobalReference(name);
		}

		public SymbolRef CreateGlobalReference(string name)
		{
			if (name == WellKnownSymbols.ENV)
				throw new InternalErrorException("_ENV passed in CreateGlobalReference");

			SymbolRef env = Find(WellKnownSymbols.ENV);
			return SymbolRef.Global(name, env);
		}


		public void ForceEnvUpValue()
		{
			Find(WellKnownSymbols.ENV);
		}

		private SymbolRef CreateUpValue(BuildTimeScope buildTimeScope, SymbolRef symb, int closuredFrame, int currentFrame)
		{
			// it's a 0-level upvalue. Just create it and we're done.
			if (closuredFrame == currentFrame)
				return m_ClosureBuilders[currentFrame + 1].CreateUpvalue(this, symb);

			SymbolRef upvalue = CreateUpValue(buildTimeScope, symb, closuredFrame, currentFrame - 1);

			return m_ClosureBuilders[currentFrame + 1].CreateUpvalue(this, upvalue);
		}

		public SymbolRef DefineLocal(string name)
		{
			return m_Frames.Last().DefineLocal(name);
		}

		public SymbolRef TryDefineLocal(string name)
		{
			return m_Frames.Last().TryDefineLocal(name);
		}

		public bool CurrentFunctionHasVarArgs()
		{
			return m_Frames.Last().HasVarArgs;
		}

		internal void DefineLabel(LabelStatement label)
		{
			m_Frames.Last().DefineLabel(label);
		}

		internal void RegisterGoto(GotoStatement gotostat)
		{
			m_Frames.Last().RegisterGoto(gotostat);
		}

	}
}
