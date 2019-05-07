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
    public partial class PasswordForm : Form
    {
        public PasswordForm()
        {
            InitializeComponent();
        }

        public string Token = "";

        private void button1_Click(object sender, EventArgs e)
        {
            Token = textBoxPassword.Text;
            Close();
        }

        private void PasswordForm_Load(object sender, EventArgs e)
        {
            Token = "";
        }
    }
}
