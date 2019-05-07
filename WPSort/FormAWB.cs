using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WPSort
{
    public partial class FormAWB : Form
    {
        public string AWBNumber = "";

        public FormAWB()
        {
            InitializeComponent();
        }

        private void FormAWB_Load(object sender, EventArgs e)
        {
            AWBNumber = "";
        }

        private void FormAWB_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!e.Cancel)
            {
                AWBNumber = textBox1.Text;
            }
        }
    }
}
