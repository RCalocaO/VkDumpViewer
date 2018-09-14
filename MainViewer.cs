using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VkDumpViewer
{
    public partial class MainViewer : Form
    {
        public MainViewer()
        {
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var OFD = new OpenFileDialog();
            var Result = OFD.ShowDialog();
            if (Result == DialogResult.OK)
            {
                this.Text = OFD.FileName;
            }
        }
    }
}
