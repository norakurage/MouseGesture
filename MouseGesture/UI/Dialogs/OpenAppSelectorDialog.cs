using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace WindowsFormsApp1.UI
{
    public class OpenAppItem
    {
        public string AppName { get; set; }
        public string AppPath { get; set; }
        public override string ToString() => AppName;
    }

    public class OpenAppSelectorDialog : Form
    {
        public string SelectedAppName { get; private set; }
        public string SelectedAppPath { get; private set; }

        private ComboBox _cmbOpenApps;
        private HashSet<string> _existingPaths;

        public OpenAppSelectorDialog(IEnumerable<string> existingPaths)
        {
            _existingPaths = new HashSet<string>(existingPaths, StringComparer.OrdinalIgnoreCase);
            InitializeUI();
            LoadOpenApps();
        }

        private void InitializeUI()
        {
            this.Text = "アプリ追加";
            this.Size = new Size(400, 180);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            Label lbl = new Label() { Left = 20, Top = 20, Text = "現在開いているアプリを選択してください:", AutoSize = true, ForeColor = Color.White };
            _cmbOpenApps = new ComboBox() { Left = 20, Top = 45, Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbOpenApps.BackColor = Color.FromArgb(45, 45, 48);
            _cmbOpenApps.ForeColor = Color.White;
            _cmbOpenApps.FlatStyle = FlatStyle.Flat;

            Button btnOk = new Button() { Text = "追加", Left = 200, Width = 80, Top = 90, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White };
            btnOk.Click += BtnOk_Click;

            Button btnCancel = new Button() { Text = "キャンセル", Left = 290, Width = 80, Top = 90, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(45, 45, 48), ForeColor = Color.White };
            
            this.Controls.Add(lbl);
            this.Controls.Add(_cmbOpenApps);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void LoadOpenApps()
        {
            var processes = Process.GetProcesses();
            var currentPid = Process.GetCurrentProcess().Id;
            var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in processes)
            {
                try
                {
                    if (p.Id != currentPid && p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                    {
                        string path = ProcessUtils.GetProcessPath((uint)p.Id);
                        if (!string.IsNullOrEmpty(path) && !addedPaths.Contains(path) && !_existingPaths.Contains(path))
                        {
                            addedPaths.Add(path);
                            string exeName = Path.GetFileNameWithoutExtension(path);
                            if (exeName.Length > 0 && char.IsLower(exeName[0])) {
                                exeName = char.ToUpper(exeName[0]) + exeName.Substring(1);
                            }

                            _cmbOpenApps.Items.Add(new OpenAppItem 
                            { 
                                AppName = exeName, 
                                AppPath = path 
                            });
                        }
                    }
                }
                catch { }
            }

            if (_cmbOpenApps.Items.Count > 0)
                _cmbOpenApps.SelectedIndex = 0;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            var selected = _cmbOpenApps.SelectedItem as OpenAppItem;
            if (selected != null)
            {
                SelectedAppName = selected.AppName;
                SelectedAppPath = selected.AppPath;
            }
            else
            {
                this.DialogResult = DialogResult.Cancel;
            }
        }
    }
}
