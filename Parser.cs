using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace VkDumpViewer
{
	public class FBase
	{
		public virtual void Fill(TreeNode Node)
		{
		}
	}

	public class FCommand : FBase
	{
	}

	public class FWriteTimestamp : FCommand
	{
		public string PipelineState;
		public string QueryPool;
		public int Query;

		public override void Fill(TreeNode Node)
		{
			Node.Nodes.Add("WriteTimestamp " + PipelineState + " Pool " + QueryPool + " Query " + Query);
		}
	}

	public class FBindPipeline : FCommand
	{
		public string BindPoint;
		public string Pipeline;

		public override void Fill(TreeNode Node)
		{
			Node.Nodes.Add("BindPipeline " + BindPoint + " " + Pipeline);
		}
	}

	public class FCmdBuffer : FBase
	{
		string CmdBuffer;
		public enum EState
		{
			Begun,
			Ended,
		}
		public EState State = EState.Begun;
		public List<FCommand> Commands = new List<FCommand>();

		public bool IsReadyToBegin()
		{
			return State == EState.Ended;
		}

		public bool IsInsideBegin()
		{
			return State == EState.Begun;
		}

		public FCmdBuffer(string InCmdBuffer)
		{
			CmdBuffer = InCmdBuffer;
		}

		public override void Fill(TreeNode Node)
		{
			foreach (var Cmd in Commands)
			{
				Cmd.Fill(Node);
			}
		}
	}

	public class FFrameThreadEntry : FBase
	{
		public int Thread = 0;
		public int Frame = 0;

		public Dictionary<string, FCmdBuffer> CmdBuffers = new Dictionary<string, FCmdBuffer>();

		public void BeginCmdBuffer(string CmdBuffer)
		{
			if (CmdBuffers.ContainsKey(CmdBuffer))
			{
				FCmdBuffer CB = CmdBuffers[CmdBuffer];
				if (!CB.IsReadyToBegin())
				{
					throw new Exception("Already begun CmdBuffer " + CmdBuffer);
				}
				CB.State = FCmdBuffer.EState.Begun;
				return;
			}
			else
			{
				var NewCmdBuffer = new FCmdBuffer(CmdBuffer);
				CmdBuffers.Add(CmdBuffer, NewCmdBuffer);
			}
		}

		public FCmdBuffer GetCmdBufferForAdd(string CmdBuffer)
		{
			if (!CmdBuffers.ContainsKey(CmdBuffer))
			{
				throw new Exception("Couldn't find CmdBuffer " + CmdBuffer);
			}

			FCmdBuffer CB = CmdBuffers[CmdBuffer];
			if (!CB.IsInsideBegin())
			{
				throw new Exception("Haven't started CmdBuffer " + CmdBuffer);
			}

			return CB;
		}

		public void EndCmdBuffer(string CmdBuffer)
		{
			if (!CmdBuffers.ContainsKey(CmdBuffer))
			{
				throw new Exception("Couldn't find CmdBuffer " + CmdBuffer);
			}
			FCmdBuffer CB = CmdBuffers[CmdBuffer];
			if (!CB.IsInsideBegin())
			{
				throw new Exception("No begin for CmdBuffer " + CmdBuffer);
			}
			CB.State = FCmdBuffer.EState.Ended;
		}
	}

	public class FParser
	{
		string[] Lines;
		int LineIndex = 0;

		string CurrentLine;
		int CurrentLineCharIndex = 0;
		char[] CurrentLineChars;
		FFrameThreadEntry CurrentFTEntry;

		public Dictionary<int, Dictionary<int, FFrameThreadEntry>> FTEntries = new Dictionary<int, Dictionary<int, FFrameThreadEntry>>();

		public FParser(string Filename)
		{
			Lines = File.ReadAllLines(Filename);
			ReadLine();
		}

		void ReadLine()
		{
			if (LineIndex < Lines.Count())
			{
				CurrentLine = Lines[LineIndex];
				CurrentLineCharIndex = 0;
				CurrentLineChars = CurrentLine.ToCharArray();
				++LineIndex;
			}
		}

		void SkipWhitespace()
		{
			while (GetCurrentChar() == ' ' || GetCurrentChar() == '\t' && CurrentLineCharIndex < CurrentLineChars.Length)
			{
				++CurrentLineCharIndex;
			}
		}

		void Match(string Keyword)
		{
			SkipWhitespace();
			//if (Keyword == CurrentLine.Substring(CurrentLineCharIndex, Keyword.Length))
			if (CurrentLine.IndexOf(Keyword, CurrentLineCharIndex, Keyword.Length) == CurrentLineCharIndex)
			{
				CurrentLineCharIndex += Keyword.Length;
			}
			else
			{
				throw new Exception("Expected '" + Keyword + "'");
			}
		}

		void Match(char c)
		{
			SkipWhitespace();
			//if (Keyword == CurrentLine.Substring(CurrentLineCharIndex, Keyword.Length))
			if (CurrentLine[CurrentLineCharIndex] == c)
			{
				++CurrentLineCharIndex;
			}
			else
			{
				throw new Exception("Expected '" + c + "'");
			}
		}

		int ParseInt()
		{
			SkipWhitespace();
			int StartInt = CurrentLineCharIndex;
			if (GetCurrentChar() == '-')
			{
				++CurrentLineCharIndex;
			}

			if (CurrentLineCharIndex == CurrentLineChars.Length)
			{
				throw new Exception("Expected integer number!");
			}

			while (GetCurrentChar() >= '0' && GetCurrentChar() <= '9' && CurrentLineCharIndex < CurrentLineChars.Length)
			{
				++CurrentLineCharIndex;
			}

			string Number = CurrentLine.Substring(StartInt, CurrentLineCharIndex - StartInt);
			int Value = 0;
			if (!int.TryParse(Number, out Value))
			{
				throw new Exception("Expected integer number!");
			}

			return Value;
		}

		void ParseThreadFrame()
		{
			Match("Thread");
			int Thread = ParseInt();
			Match(',');
			Match("Frame");
			int Frame = ParseInt();
			Match(':');

			if (!FTEntries.ContainsKey(Frame))
			{
				FTEntries.Add(Frame, new Dictionary<int, FFrameThreadEntry>());
			}

			if (FTEntries[Frame].ContainsKey(Thread))
			{
				CurrentFTEntry = FTEntries[Frame][Thread];
			}
			else
			{
				CurrentFTEntry = new FFrameThreadEntry();
				CurrentFTEntry.Thread = Thread;
				CurrentFTEntry.Frame = Frame;
				FTEntries[Frame].Add(Thread, CurrentFTEntry);
				Console.WriteLine((LineIndex + 1) + ": Thread " + Thread + ", Frame " + Frame);
			}

			ReadLine();
		}

		bool HasCommands()
		{
			return CurrentLine != "" && LineIndex < Lines.Length;
		}

		bool PeekAndAdvance(string Keyword)
		{
			if (CurrentLineCharIndex + Keyword.Length <= CurrentLine.Length && CurrentLine.IndexOf(Keyword, CurrentLineCharIndex, Keyword.Length) != -1)
			{
				CurrentLineCharIndex += Keyword.Length;
				return true;
			}

			return false;
		}

		void ParseEntry()
		{
			try
			{
				ParseThreadFrame();

				while (HasCommands())
				{
					ParseCommand();
				}

				ReadLine();
			}
			catch (Exception E)
			{
				throw E;
			}
		}

		public bool Parse()
		{
			try
			{
				while (LineIndex < Lines.Length)
				{
					ParseEntry();
				}
			}
			catch (Exception)
			{
				return false;
			}

			return true;
		}


		public void PopulateTreeView(TreeView MainTreeView)
		{
			foreach (var EntryList in FTEntries)
			{
				var TopNode = MainTreeView.Nodes.Add("Frame " + EntryList.Key);
				foreach (var SubEntryList in EntryList.Value)
				{
					var SubNode = TopNode.Nodes.Add("Thread " + SubEntryList.Key);
					foreach (var CmdBuffer in SubEntryList.Value.CmdBuffers)
					{
						var CmdBufNode = SubNode.Nodes.Add("CB: " + CmdBuffer.Key);
						CmdBuffer.Value.Fill(CmdBufNode);
					}
				}
			}
		}

		string ParseSimpleAssignmentHandle(string LHS, string RHSType)
		{
			SkipWhitespace();
			Match(LHS);
			Match(':');
			SkipWhitespace();
			Match(RHSType);
			Match('=');
			string Value = ParseHandle();
			ReadLine();
			return Value;
		}

		string ParseSimpleAssignmentEnum(string LHS, string RHSType)
		{
			SkipWhitespace();
			Match(LHS);
			Match(':');
			SkipWhitespace();
			Match(RHSType);
			Match('=');
			string Value = ParseIdentifier();
			Match('(');
			int Numeric = ParseInt();
			Match(')');
			ReadLine();
			return Value;
		}

		int ParseSimpleAssignmentInt(string LHS, string RHSType)
		{
			SkipWhitespace();
			Match(LHS);
			Match(':');
			SkipWhitespace();
			Match(RHSType);
			Match('=');
			int Value = ParseInt();
			ReadLine();
			return Value;
		}

		string ParseCommandBuffer()
		{
			return ParseSimpleAssignmentHandle("commandBuffer", "VkCommandBuffer");
		}

		string ParsePipeline()
		{
			return ParseSimpleAssignmentHandle("pipeline", "VkPipeline");
		}

		string ParsePipelineBindPoint()
		{
			string Bind = ParseSimpleAssignmentEnum("pipelineBindPoint", "VkPipelineBindPoint");
			if (Bind.StartsWith("VK_PIPELINE_BIND_POINT_"))
			{
				Bind = Bind.Substring(23);
			}
			return Bind;
		}

		static bool IsAlpha(char c)
		{
			return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
		}
		static bool IsNumeric(char c)
		{
			return (c >= '0' && c <= '9');
		}

		static bool IsHex(char c)
		{
			return (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F') || IsNumeric(c);
		}

		static bool IsAlphaNumeric(char c)
		{
			return IsNumeric(c) || IsAlpha(c);
		}

		char GetCurrentChar()
		{
			if (CurrentLineCharIndex == CurrentLineChars.Length)
			{
				return '\0';
			}

			return CurrentLineChars[CurrentLineCharIndex];
		}

		string ParseIdentifier()
		{
			SkipWhitespace();
			int Start = CurrentLineCharIndex;
			while (IsAlpha(GetCurrentChar()) || GetCurrentChar() == '_')
			{
				++CurrentLineCharIndex;
			}
			while (IsAlphaNumeric(GetCurrentChar()) || GetCurrentChar() == '_')
			{
				++CurrentLineCharIndex;
			}

			if (CurrentLineCharIndex == Start)
			{
				throw new Exception("Expected identifier!");
			}
			return CurrentLine.Substring(Start, CurrentLineCharIndex - Start);
		}

		string ParseHandle()
		{
			SkipWhitespace();
			int Start = CurrentLineCharIndex;

			if (GetCurrentChar() == '0')
			{
				while (CurrentLineCharIndex < CurrentLineChars.Length && IsHex(GetCurrentChar()))
				{
					++CurrentLineCharIndex;
				}
			}

			if (CurrentLineCharIndex == Start)
			{
				throw new Exception("Expected Vk handle!");
			}
			return CurrentLine.Substring(Start, CurrentLineCharIndex - Start);
		}

		string ParsePipelineStageBits()
		{
			SkipWhitespace();
			Match("pipelineStage:");
			SkipWhitespace();
			Match("VkPipelineStageFlagBits = ");
			SkipWhitespace();
			ParseInt();
			Match('(');
			string Stage = ParseIdentifier();
			if (Stage.StartsWith("VK_PIPELINE_STAGE_"))
			{
				Stage = Stage.Substring(18);
				if (Stage.EndsWith("_BIT"))
				{
					Stage = Stage.Substring(0, Stage.Length - 4);
				}
			}
			Match(')');
			ReadLine();

			return Stage;
		}

		string ParseQueryPool()
		{
			return ParseSimpleAssignmentHandle("queryPool", "VkQueryPool");
		}

		int ParseQuery()
		{
			return ParseSimpleAssignmentInt("query", "uint32_t");
		}

		void ParseCommand()
		{
			try
			{
				SkipWhitespace();
				if (PeekAndAdvance("vkBeginCommandBuffer("))
				{
					ReadLine();
					//var CmdBuffer = new FBeginCmdBuffer();
					string CmdBuffer = ParseCommandBuffer();
					ReadLine();
					ReadLine();
					ReadLine();
					ReadLine();
					ReadLine();

					CurrentFTEntry.BeginCmdBuffer(CmdBuffer);
				}
				else if (PeekAndAdvance("vkEndCommandBuffer("))
				{
					ReadLine();

					//var CmdBuffer = new FEndCmdBuffer();
					//CurrentFTEntry.Items.Add(CmdBuffer);
					string CmdBuffer = ParseCommandBuffer();
					CurrentFTEntry.EndCmdBuffer(CmdBuffer);
				}
				else if (PeekAndAdvance("vkCmdWriteTimestamp("))
				{
					ReadLine();

					var WT = new FWriteTimestamp();

					string CmdBuffer = ParseCommandBuffer();
					WT.PipelineState = ParsePipelineStageBits();
					WT.QueryPool = ParseQueryPool();
					WT.Query = ParseQuery();

					FCmdBuffer CB = CurrentFTEntry.GetCmdBufferForAdd(CmdBuffer);
					CB.Commands.Add(WT);
				}
				else if (PeekAndAdvance("vkCmdBindPipeline("))
				{
					ReadLine();

					var WT = new FBindPipeline();

					string CmdBuffer = ParseCommandBuffer();
					WT.BindPoint = ParsePipelineBindPoint();
					WT.Pipeline = ParsePipeline();

					FCmdBuffer CB = CurrentFTEntry.GetCmdBufferForAdd(CmdBuffer);
					CB.Commands.Add(WT);
				}

				ReadLine();
			}
			catch (Exception E)
			{
				throw E;
			}
		}
	}
}
