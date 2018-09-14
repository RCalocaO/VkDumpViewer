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
	public partial class MainViewer : Form
	{
		public MainViewer(string[] Args)
		{
			InitializeComponent();

			if (Args.Length > 0)
			{
				if (Parse(Args[0]))
				{
					Invalidate();
				}
			}
		}

		bool Parse(string Filename)
		{
			Console.WriteLine("Parsing " + Filename + "...");
			var Parser = new FParser(Filename);
			if (Parser.Parse())
			{
				foreach (var EntryList in Parser.Entries)
				{
					var TopNode = MainTreeView.Nodes.Add("Frame " +EntryList.Key);
					foreach (var SubEntryList in EntryList.Value)
					{
						TopNode.Nodes.Add("Thread " + SubEntryList.Key);
					}
				}

				this.Text = "VkDumpViewer - " + Filename;
				MainTreeView.ExpandAll();

				return true;
			}

			return false;
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var OFD = new OpenFileDialog();
			OFD.InitialDirectory = Directory.GetCurrentDirectory();
			OFD.RestoreDirectory = true;
			var Result = OFD.ShowDialog();
			if (Result == DialogResult.OK)
			{
				if (Parse(OFD.FileName))
				{
					Invalidate();
				}
			}
		}
	}

	public struct FEntry
	{
	}

	public class FParser
	{
		string[] Lines;
		int LineIndex = 0;

		string CurrentLine;
		int CurrentLineCharIndex = 0;
		char[] CurrentLineChars;

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
			if (Keyword == CurrentLine.Substring(CurrentLineCharIndex, Keyword.Length))
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

			if (!Entries.ContainsKey(Frame))
			{
				Entries.Add(Frame, new Dictionary<int, FEntry>());
			}

			if (!Entries[Frame].ContainsKey(Thread))
			{
				Entries[Frame].Add(Thread, new FEntry());
				Console.WriteLine((LineIndex + 1) + ": Thread " + Thread + ", Frame " + Frame);
			}

			ReadLine();
		}

		bool HasCommands()
		{
			return CurrentLine != "" && LineIndex < Lines.Length;
		}

		void ParseEntry()
		{
			try
			{
				ParseThreadFrame();

				while (HasCommands())
				{
					ReadLine();
				}

				ReadLine();
			}
			catch (Exception)
			{
				return;
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
	}
}
