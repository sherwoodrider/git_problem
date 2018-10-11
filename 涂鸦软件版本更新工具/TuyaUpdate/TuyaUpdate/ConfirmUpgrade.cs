using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TuyaUpdate.Properties;
using System.IO;

namespace TuyaUpdate
{
    public partial class ConfirmUpgrade : Form
    {
        public ConfirmUpgrade()
        {
            InitializeComponent();
        }

        private void bt_OK_Click(object sender, EventArgs e)
        {
            Settings.Default.needUpgrade = true;
            this.Close();
        }

        private void bt_Cancel_Click(object sender, EventArgs e)
        {
            Settings.Default.needUpgrade = false;
            this.Close();
        }

        private void ConfirmUpgrade_Load(object sender, EventArgs e)
        {
            lb_version.Text = Settings.Default.versionText;
            richTextBox1.Text = Settings.Default.upgradeCnDesc;
        }
     
    }
}
