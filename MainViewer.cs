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
				Parser.PopulateTreeView(MainTreeView);

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
}
