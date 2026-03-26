using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace WindowsFormsApp1.UI
{
    public static class GroupEditorDialogs
    {
        public static string ShowCreateGroupDialog(IWin32Window owner, IEnumerable<string> existingGroupNames)
        {
            using (Form inputForm = new Form())
            {
                inputForm.Text = "グループ作成";
                inputForm.Size = new Size(300, 150);
                inputForm.StartPosition = FormStartPosition.CenterParent;
                inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                inputForm.MaximizeBox = false;

                inputForm.BackColor = Color.FromArgb(30, 30, 30);
                inputForm.ForeColor = Color.White;

                Label lbl = new Label() { Left = 20, Top = 20, Text = "グループ名:", AutoSize = true, ForeColor = Color.White };
                TextBox txt = new TextBox() { Left = 20, Top = 50, Width = 240, BackColor = Color.FromArgb(45, 45, 48), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
                Button btnOk = new Button() { Text = "OK", Left = 180, Width = 80, Top = 80, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White };
                
                inputForm.Controls.Add(lbl);
                inputForm.Controls.Add(txt);
                inputForm.Controls.Add(btnOk);
                inputForm.AcceptButton = btnOk;

                if (inputForm.ShowDialog(owner) == DialogResult.OK)
                {
                    string name = txt.Text.Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        if (existingGroupNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                        {
                            MessageBox.Show("同じ名前のグループが既に存在します。");
                            return null;
                        }
                        return name;
                    }
                }
                return null;
            }
        }

        public static bool ShowAppSelectionDialog(IWin32Window owner, GroupProfile currentProfile, IEnumerable<AppProfile> availableApps)
        {
            if (currentProfile == null) return false;

            using (Form listForm = new Form())
            {
                listForm.Text = "アプリ選択";
                listForm.Size = new Size(400, 400);
                listForm.StartPosition = FormStartPosition.CenterParent;
                
                listForm.BackColor = Color.FromArgb(30, 30, 30);
                listForm.ForeColor = Color.White;

                CheckedListBox chkList = new CheckedListBox();
                chkList.Dock = DockStyle.Fill;
                chkList.CheckOnClick = true;
                chkList.BackColor = Color.FromArgb(45, 45, 48);
                chkList.ForeColor = Color.White;
                chkList.BorderStyle = BorderStyle.None;

                var appsList = availableApps.ToList();
                foreach(var app in appsList)
                {
                    bool isChecked = currentProfile.TargetAppPaths.Contains(app.AppPath);
                    chkList.Items.Add(app.AppName + " (" + Path.GetFileName(app.AppPath) + ")", isChecked); 
                }

                Panel pnlBottom = new Panel();
                pnlBottom.Dock = DockStyle.Bottom;
                pnlBottom.Height = 50;
                pnlBottom.BackColor = Color.FromArgb(30, 30, 30);

                Button btnOk = new Button() { Text = "OK", Dock = DockStyle.Right, Width = 80, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White };
                pnlBottom.Controls.Add(btnOk);

                listForm.Controls.Add(chkList);
                listForm.Controls.Add(pnlBottom);

                if (listForm.ShowDialog(owner) == DialogResult.OK)
                {
                    currentProfile.TargetAppPaths.Clear();
                    for(int i = 0; i < chkList.Items.Count; i++)
                    {
                        if (chkList.GetItemChecked(i))
                        {
                             var app = appsList[i]; 
                             currentProfile.TargetAppPaths.Add(app.AppPath);
                        }
                    }
                    return true;
                }
                return false;
            }
        }
    }
}
