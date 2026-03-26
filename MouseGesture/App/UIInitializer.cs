using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

namespace WindowsFormsApp1
{
    public partial class Form1
    {
        private void InitializeCustomUI()
        {
            // Create TabControl
            _tabControl = new TabControl();
            _tabControl.Dock = DockStyle.Fill;
            _tabControl.Location = new Point(0, 50); // Below label
            _tabControl.Size = new Size(this.ClientSize.Width, this.ClientSize.Height - 50);
            
            // Create Global Tab
            _tabGlobal = new TabPage("共通設定");
            _tabGlobal.BackColor = Color.FromArgb(30, 30, 30);
            
            // Create App Tab
            _tabApp = new TabPage("アプリ別設定");
            _tabApp.BackColor = Color.FromArgb(30, 30, 30);
            
            // Move existing dgvGestures to Global Tab
            this.Controls.Remove(dgvGestures);

            Panel panelGlobalTop = new Panel();
            panelGlobalTop.Dock = DockStyle.Top;
            panelGlobalTop.Height = 40;
            panelGlobalTop.BackColor = Color.FromArgb(30, 30, 30);

            _chkAutoStart = new CheckBox();
            _chkAutoStart.Text = "コンピュータ起動時に自動で起動する";
            _chkAutoStart.ForeColor = Color.White;
            _chkAutoStart.Location = new Point(10, 10);
            _chkAutoStart.AutoSize = true;
            _chkAutoStart.Checked = _trayIconManager != null ? _trayIconManager.GetAutoStartStatus(Application.ExecutablePath) : false;
            if (_trayIconManager == null) 
            {
                // Fallback for before initialization
                var tempManager = new TrayIconManager(null, "", null, null);
                _chkAutoStart.Checked = tempManager.GetAutoStartStatus(Application.ExecutablePath);
                tempManager.Dispose();
            }
            _chkAutoStart.CheckedChanged += (s, e) =>
            {
                _trayIconManager?.RegisterAutoStart(_chkAutoStart.Checked, Application.ExecutablePath);
            };
            
            panelGlobalTop.Controls.Add(_chkAutoStart);

            _tabGlobal.Controls.Add(panelGlobalTop);

            _tabGlobal.Controls.Add(dgvGestures);
            dgvGestures.Dock = DockStyle.Fill;
            dgvGestures.BringToFront(); // Ensure it fills remaining space
            
            // Setup App Tab UI
            Panel panelTop = new Panel();
            panelTop.Dock = DockStyle.Top;
            panelTop.Height = 40;
            panelTop.BackColor = Color.FromArgb(30, 30, 30);

            _lblAppHint = new Label();
            _lblAppHint.Text = "アプリ:";
            _lblAppHint.ForeColor = Color.White;
            _lblAppHint.Location = new Point(10, 12);
            _lblAppHint.AutoSize = true;

            _cmbApps = new ComboBox();
            _cmbApps.Location = new Point(60, 8);
            _cmbApps.Width = 150;
            _cmbApps.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbApps.SelectedIndexChanged += CmbApps_SelectedIndexChanged;

            _btnSelectWindow = new Button();
            _btnSelectWindow.Text = "追加";
            _btnSelectWindow.Location = new Point(220, 7);
            _btnSelectWindow.Size = new Size(50, 25);
            _btnSelectWindow.FlatStyle = FlatStyle.Flat;
            _btnSelectWindow.BackColor = Color.FromArgb(0, 122, 204);
            _btnSelectWindow.ForeColor = Color.White;
            _btnSelectWindow.Click += BtnSelectWindow_Click;

            _btnRemoveApp = new Button();
            _btnRemoveApp.Text = "削除";
            _btnRemoveApp.Location = new Point(280, 7);
            _btnRemoveApp.Size = new Size(50, 25);
            _btnRemoveApp.FlatStyle = FlatStyle.Flat;
            _btnRemoveApp.BackColor = Color.IndianRed;
            _btnRemoveApp.ForeColor = Color.White;
            _btnRemoveApp.Click += BtnRemoveApp_Click;

            _chkDisableApp = new CheckBox();
            _chkDisableApp.Text = "このアプリでは無効にする";
            _chkDisableApp.ForeColor = Color.White;
            _chkDisableApp.Location = new Point(340, 10);
            _chkDisableApp.AutoSize = true;
            _chkDisableApp.CheckedChanged += (s, e) =>
            {
                if (_profileManager.CurrentAppProfile != null)
                {
                    _profileManager.CurrentAppProfile.Disabled = _chkDisableApp.Checked;
                    _profileManager.SaveGestures();
                }
            };

            panelTop.Controls.Add(_lblAppHint);
            panelTop.Controls.Add(_cmbApps);
            panelTop.Controls.Add(_btnSelectWindow);
            panelTop.Controls.Add(_btnRemoveApp);
            panelTop.Controls.Add(_chkDisableApp);
            
            _dgvAppGestures = new DataGridView();
            _dgvAppGestures.Dock = DockStyle.Fill;
            _dgvAppGestures.BackgroundColor = Color.FromArgb(30, 30, 30);
            
            _tabApp.Controls.Add(_dgvAppGestures);
            _tabApp.Controls.Add(panelTop);
            

            // Create Group Tab
            _tabGroup = new TabPage("グループ設定");
            _tabGroup.BackColor = Color.FromArgb(30, 30, 30);

            // Setup Group Tab UI
            Panel panelGroupTop = new Panel();
            panelGroupTop.Dock = DockStyle.Top;
            panelGroupTop.Height = 40;
            panelGroupTop.BackColor = Color.FromArgb(30, 30, 30);

            _lblGroupHint = new Label();
            _lblGroupHint.Text = "グループ:";
            _lblGroupHint.ForeColor = Color.White;
            _lblGroupHint.Location = new Point(10, 12);
            _lblGroupHint.AutoSize = true;

            _cmbGroups = new ComboBox();
            _cmbGroups.Location = new Point(80, 8);
            _cmbGroups.Width = 150;
            _cmbGroups.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbGroups.SelectedIndexChanged += CmbGroups_SelectedIndexChanged;

            _btnAddGroup = new Button();
            _btnAddGroup.Text = "作成";
            _btnAddGroup.Location = new Point(240, 7);
            _btnAddGroup.Size = new Size(50, 25);
            _btnAddGroup.FlatStyle = FlatStyle.Flat;
            _btnAddGroup.BackColor = Color.FromArgb(0, 122, 204);
            _btnAddGroup.ForeColor = Color.White;
            _btnAddGroup.Click += BtnAddGroup_Click;

            _btnEditGroupApps = new Button();
            _btnEditGroupApps.Text = "アプリ選択";
            _btnEditGroupApps.Location = new Point(300, 7);
            _btnEditGroupApps.Size = new Size(80, 25);
            _btnEditGroupApps.FlatStyle = FlatStyle.Flat;
            _btnEditGroupApps.BackColor = Color.FromArgb(45, 45, 48);
            _btnEditGroupApps.ForeColor = Color.White;
            _btnEditGroupApps.Click += BtnEditGroupApps_Click;

            _btnRemoveGroup = new Button();
            _btnRemoveGroup.Text = "削除";
            _btnRemoveGroup.Location = new Point(390, 7);
            _btnRemoveGroup.Size = new Size(60, 25);
            _btnRemoveGroup.FlatStyle = FlatStyle.Flat;
            _btnRemoveGroup.BackColor = Color.IndianRed;
            _btnRemoveGroup.ForeColor = Color.White;
            _btnRemoveGroup.Click += BtnRemoveGroup_Click;

            panelGroupTop.Controls.Add(_lblGroupHint);
            panelGroupTop.Controls.Add(_cmbGroups);
            panelGroupTop.Controls.Add(_btnAddGroup);
            panelGroupTop.Controls.Add(_btnEditGroupApps);
            panelGroupTop.Controls.Add(_btnRemoveGroup);

            _dgvGroupGestures = new DataGridView();
            _dgvGroupGestures.Dock = DockStyle.Fill;
            _dgvGroupGestures.BackgroundColor = Color.FromArgb(30, 30, 30);

            _tabGroup.Controls.Add(_dgvGroupGestures);
            _tabGroup.Controls.Add(panelGroupTop);

            _tabControl.Controls.Add(_tabGlobal);
            _tabControl.Controls.Add(_tabGroup);
            _tabControl.Controls.Add(_tabApp);
            
            this.Controls.Add(_tabControl);
            
            // Adjust Label Logic
            lblGesture.Visible = false; // Hide old label
            
            // Initialize StatusStrip
            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel();
            _statusStrip.Items.Add(_statusLabel);
            _statusStrip.Dock = DockStyle.Bottom;
            _statusStrip.SizingGrip = false;
            _statusLabel.Spring = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            
            this.Controls.Add(_statusStrip);
            _statusStrip.SendToBack(); // Docking priority: Bottom of Z-Order dock first.
            _tabControl.BringToFront(); // Fill takes remaining space.
            
            // Populate App Combo
            RefreshAppList();
            RefreshGroupList();
        }

        private void SetupGridProperties(DataGridView dgv)
        {
            dgv.AllowUserToAddRows = false;
            _gestureEditController.Attach(dgv);

            dgv.DataBindingComplete += (s, e) =>
            {
                if (dgv.Columns["Gesture"] != null) dgv.Columns["Gesture"].ReadOnly = true;
                if (dgv.Columns["KeyBinding"] != null) dgv.Columns["KeyBinding"].ReadOnly = true;
                // Ensure Name is editable if needed, though default is typically false if whole row isn't readonly
                // But generally only set ReadOnly=true for specific columns
            };

            if (dgv.Columns["Delete"] == null)
            {
                DataGridViewButtonColumn btn = new DataGridViewButtonColumn();
                btn.Name = "Delete";
                btn.HeaderText = "削除";
                btn.Text = "×";
                btn.UseColumnTextForButtonValue = true;
                btn.Width = 50;
                btn.FlatStyle = FlatStyle.Flat;
                btn.DefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
                btn.DefaultCellStyle.ForeColor = Color.IndianRed;
                dgv.Columns.Add(btn);
            }
        }

        private void RefreshAppList()
        {
            _cmbApps.Items.Clear();
            foreach (var app in _profileManager.AppProfiles.Where(a => !string.IsNullOrEmpty(a.AppPath)))
            {
                _cmbApps.Items.Add(app);
            }
            _cmbApps.DisplayMember = "AppName";
        }

        private void RefreshGroupList()
        {
            _cmbGroups.Items.Clear();
            foreach (var group in _profileManager.GroupProfiles)
            {
                _cmbGroups.Items.Add(group);
            }
            _cmbGroups.DisplayMember = "GroupName";
            if (_cmbGroups.Items.Count > 0)
                _cmbGroups.SelectedIndex = 0;
            else
            {
                _profileManager.SelectGroupProfile(null);
                _dgvGroupGestures?.Refresh();
            }
        }

        private void InitializeAddButton()
        {
            // Add Button (Global)
            _btnAddGesture = new Button();
            _btnAddGesture.Text = "ジェスチャーを追加";
            _btnAddGesture.Size = new Size(180, 35);
            // Will position later or relative
            
            _btnAddGesture.FlatStyle = FlatStyle.Flat;
            _btnAddGesture.FlatAppearance.BorderSize = 0;
            _btnAddGesture.BackColor = Color.FromArgb(0, 122, 204);
            _btnAddGesture.ForeColor = Color.White;
            _btnAddGesture.Font = new Font("Meiryo UI", 10, FontStyle.Bold); 
            _btnAddGesture.Cursor = Cursors.Hand;
            _btnAddGesture.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnAddGesture.Click += BtnAddGesture_Click;
            
            // Add to Global Tab
            _btnAddGesture.Location = new Point(_tabGlobal.Width - 220, _tabGlobal.Height - 50);
            _tabGlobal.Controls.Add(_btnAddGesture);
            _btnAddGesture.BringToFront();

            // Add Button (App)
            _btnAddAppGesture = new Button();
            _btnAddAppGesture.Text = "ジェスチャーを追加";
            _btnAddAppGesture.Size = new Size(180, 35);
            _btnAddAppGesture.FlatStyle = FlatStyle.Flat;
            _btnAddAppGesture.FlatAppearance.BorderSize = 0;
            _btnAddAppGesture.BackColor = Color.FromArgb(0, 122, 204);
            _btnAddAppGesture.ForeColor = Color.White;
            _btnAddAppGesture.Font = new Font("Meiryo UI", 10, FontStyle.Bold);
            _btnAddAppGesture.Cursor = Cursors.Hand;
            _btnAddAppGesture.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnAddAppGesture.Click += (s, e) => 
            {
                 if (_profileManager.CurrentAppProfile == null) 
                 {
                     MessageBox.Show("アプリを選択してください。");
                     return;
                 }
                 _profileManager.CurrentAppGestureMaps.Add(new GestureMap());
                 if (_dgvAppGestures.Rows.Count > 0)
                     _dgvAppGestures.FirstDisplayedScrollingRowIndex = _dgvAppGestures.Rows.Count - 1;
                 
                 UpdateGestureLabel("ジェスチャーフィールドをダブルクリックで追加");
            };
            
            _btnAddAppGesture.Location = new Point(_tabApp.Width - 220, _tabApp.Height - 50);
            _tabApp.Controls.Add(_btnAddAppGesture);
            _btnAddAppGesture.BringToFront();

            // Add Button (Group)
            _btnAddGroupGesture = new Button();
            _btnAddGroupGesture.Text = "ジェスチャーを追加";
            _btnAddGroupGesture.Size = new Size(180, 35);
            _btnAddGroupGesture.FlatStyle = FlatStyle.Flat;
            _btnAddGroupGesture.FlatAppearance.BorderSize = 0;
            _btnAddGroupGesture.BackColor = Color.FromArgb(0, 122, 204);
            _btnAddGroupGesture.ForeColor = Color.White;
            _btnAddGroupGesture.Font = new Font("Meiryo UI", 10, FontStyle.Bold);
            _btnAddGroupGesture.Cursor = Cursors.Hand;
            _btnAddGroupGesture.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnAddGroupGesture.Click += (s, e) => 
            {
                 if (_profileManager.CurrentGroupProfile == null) 
                 {
                     MessageBox.Show("グループを選択してください。");
                     return;
                 }
                 _profileManager.CurrentGroupGestureMaps.Add(new GestureMap());
                 if (_dgvGroupGestures.Rows.Count > 0)
                     _dgvGroupGestures.FirstDisplayedScrollingRowIndex = _dgvGroupGestures.Rows.Count - 1;
                 
                 UpdateGestureLabel("ジェスチャーフィールドをダブルクリックで追加");
            };
            
            _btnAddGroupGesture.Location = new Point(_tabGroup.Width - 220, _tabGroup.Height - 50);
            _tabGroup.Controls.Add(_btnAddGroupGesture);
            _btnAddGroupGesture.BringToFront();
        }

        private void ApplyModernTheme()
        {
            // Form
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Meiryo UI", 10); 
            this.Text = "マウスジェスチャーアプリ";

            // Grid Styling Helper
            Action<DataGridView> styleGrid = (dgv) => 
            {
                dgv.BackgroundColor = Color.FromArgb(30, 30, 30);
                dgv.BorderStyle = BorderStyle.FixedSingle;
                dgv.GridColor = Color.FromArgb(50, 50, 50);
                dgv.EnableHeadersVisualStyles = false;
                dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 122, 204);
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 122, 204);
                dgv.RowHeadersVisible = false;
                dgv.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
                dgv.DefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
                dgv.DefaultCellStyle.ForeColor = Color.White;
                dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(62, 62, 66);
                dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            };

            styleGrid(dgvGestures);
            if (_dgvAppGestures != null) styleGrid(_dgvAppGestures);
            if (_dgvGroupGestures != null) styleGrid(_dgvGroupGestures);

            // Label
            lblGesture.ForeColor = Color.White;

            // StatusStrip
            if (_statusStrip != null)
            {
                _statusStrip.BackColor = Color.FromArgb(45, 45, 48);
                _statusStrip.ForeColor = Color.White;
            }
            
            // Adjust buttons if needed
             if (_btnAddGesture != null && _tabGlobal != null)
            {
                 _btnAddGesture.Location = new Point(_tabGlobal.Width - 220, _tabGlobal.Height - 50);
            }
             if (_btnAddAppGesture != null && _tabApp != null)
            {
                 _btnAddAppGesture.Location = new Point(_tabApp.Width - 220, _tabApp.Height - 50);
            }
             if (_btnAddGroupGesture != null && _tabGroup != null)
            {
                 _btnAddGroupGesture.Location = new Point(_tabGroup.Width - 220, _tabGroup.Height - 50);
            }
        }

        private void StyleTextBox(TextBox txt)
        {
            txt.BackColor = Color.FromArgb(45, 45, 48);
            txt.ForeColor = Color.White;
            txt.BorderStyle = BorderStyle.FixedSingle;
        }
    }
}
