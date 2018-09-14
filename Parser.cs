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
		public string CmdBuffer;
	}

	public class FBeginCmdBuffer : FCommand
	{
		public override void Fill(TreeNode Node)
		{
			Node.Nodes.Add(CmdBuffer + ": BEGIN");
		}
	}

	public class FEndCmdBuffer : FCommand
	{
		public override void Fill(TreeNode Node)
		{
			Node.Nodes.Add(CmdBuffer + ": END");
		}
	}

	public class FEntry
	{
		public List<FBase> Items = new List<FBase>();
	}

	public class FParser
	{
		string[] Lines;
		int LineIndex = 0;

		string CurrentLine;
		int CurrentLineCharIndex = 0;
		char[] CurrentLineChars;
		FEntry CurrentEntry;

		public Dictionary<int, Dictionary<int, FEntry>> Entries = new Dictionary<int, Dictionary<int, FEntry>>();

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
			while (CurrentLineChars[CurrentLineCharIndex] == ' ' || CurrentLineChars[CurrentLineCharIndex] == '\t' && CurrentLineCharIndex < CurrentLineChars.Length)
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

		int ParseInt()
		{
			SkipWhitespace();
			int StartInt = CurrentLineCharIndex;
			if (CurrentLineChars[CurrentLineCharIndex] == '-')
			{
				++CurrentLineCharIndex;
			}

			if (CurrentLineCharIndex == CurrentLineChars.Length)
			{
				throw new Exception("Expected integer number!");
			}

			while (CurrentLineChars[CurrentLineCharIndex] >= '0' && CurrentLineChars[CurrentLineCharIndex] <= '9' && CurrentLineCharIndex < CurrentLineChars.Length)
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
			Match(",");
			Match("Frame");
			int Frame = ParseInt();
			Match(":");

			if (!Entries.ContainsKey(Frame))
			{
				Entries.Add(Frame, new Dictionary<int, FEntry>());
			}

			if (Entries[Frame].ContainsKey(Thread))
			{
				CurrentEntry = Entries[Frame][Thread];
			}
			else
			{
				CurrentEntry = new FEntry();
				Entries[Frame].Add(Thread, CurrentEntry);
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
			catch(Exception E)
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
			foreach (var EntryList in Entries)
			{
				var TopNode = MainTreeView.Nodes.Add("Frame " + EntryList.Key);
				foreach (var SubEntryList in EntryList.Value)
				{
					var SubNode = TopNode.Nodes.Add("Thread " + SubEntryList.Key);
					foreach (var Item in SubEntryList.Value.Items)
					{
						Item.Fill(SubNode);
					}
				}
			}
		}

		string ParseCommandBuffer()
		{
			SkipWhitespace();
			Match("commandBuffer:");
			SkipWhitespace();
			Match("VkCommandBuffer = ");
			string CmdBuffer = CurrentLine.Substring(CurrentLineCharIndex);
			ReadLine();
			return CmdBuffer;
		}

		void ParseCommand()
		{
			SkipWhitespace();
			if (PeekAndAdvance("vkBeginCommandBuffer("))
			{
				ReadLine();
				var CmdBuffer = new FBeginCmdBuffer();
				CurrentEntry.Items.Add(CmdBuffer);
				CmdBuffer.CmdBuffer = ParseCommandBuffer();
				ReadLine();
				ReadLine();
				ReadLine();
				ReadLine();
				ReadLine();
			}
			else if (PeekAndAdvance("vkEndCommandBuffer("))
			{
				ReadLine();

				var CmdBuffer = new FEndCmdBuffer();
				CurrentEntry.Items.Add(CmdBuffer);
				CmdBuffer.CmdBuffer = ParseCommandBuffer();
			}

			ReadLine();
		}
	}
}
