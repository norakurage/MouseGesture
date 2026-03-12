using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Xml.Serialization;
using System.Runtime.InteropServices;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private string SettingsFilePath => Path.Combine(Application.StartupPath, "gestures.xml");

        // 新しいコンポーネント
        private ProfileManager _profileManager;
        private GestureEngine _gestureEngine;
        private TrayIconManager _trayIconManager;

        // UI Controls
        private Button _btnAddGesture;
        private Button _btnAddAppGesture;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;
        
        // Real-time feedback
        private Form _feedbackForm;
        private Label _feedbackLabel;
        private Timer _feedbackFadeTimer;

        // UI Controls - Tabs
        private TabControl _tabControl;
        private TabPage _tabGlobal;
        private TabPage _tabGroup;
        private TabPage _tabApp;
        
        // UI Controls - App Tab
        private ComboBox _cmbApps;
        private Button _btnRemoveApp;
        private DataGridView _dgvAppGestures;
        private Label _lblAppHint;
        private CheckBox _chkDisableApp;

        // UI Controls - Group Tab
        private ComboBox _cmbGroups;
        private Button _btnAddGroup;
        private Button _btnRemoveGroup;
        private Button _btnEditGroupApps;
        private DataGridView _dgvGroupGestures;
        private Label _lblGroupHint;
        private Button _btnAddGroupGesture;

        // Window Selection
        private Button _btnSelectWindow;

        // Editing State
        private int _editingRowIndex = -1;
        private bool _isRecordingGesture = false;
        private bool _isListeningForKey = false;
        private DataGridView _editingGrid; // Track which grid is editing
        private BindingList<GestureMap> _editingList; // Track which list is being edited
        private volatile bool _isClosing = false;

        // System Tray
        private bool _realClose = false; // 本当に終了する場合のみ true
        
        // Auto Start
        private CheckBox _chkAutoStart;

        // シングルインスタンス制御
        private System.Threading.EventWaitHandle _showWindowWaitHandle;
        private System.Threading.RegisteredWaitHandle _registeredWaitHandle;

        public Form1()

        {
            InitializeComponent();
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private void ShowWindowCallback(object state, bool timedOut)
        {
            if (this.IsHandleCreated)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    RestoreFromTray();
                });
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_trayIconManager != null && !_trayIconManager.RealClose && e.CloseReason == CloseReason.UserClosing)
            {
                // ×ボタンはトレイへ格納するだけ
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                // 本当に終了する
                _isClosing = true;
                _gestureEngine?.Stop();
                
                _registeredWaitHandle?.Unregister(null);
                _showWindowWaitHandle?.Dispose();

                _profileManager?.SaveGestures();
                _trayIconManager?.Dispose();
                _gestureEngine?.Dispose();

                base.OnFormClosing(e);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (IsDesignMode()) return;

            // シングルインスタンス用のイベント待機ハンドル
            _showWindowWaitHandle = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, "MouseGestureApp_ShowWindowEvent");
            _registeredWaitHandle = System.Threading.ThreadPool.RegisterWaitForSingleObject(_showWindowWaitHandle, ShowWindowCallback, null, -1, false);

            _profileManager = new ProfileManager(SettingsFilePath);
            _profileManager.LoadGestures();

            InitializeCustomUI(); // Create Tabs and move controls
            InitializeAddButton(); // Add buttons
            ApplyModernTheme(); // Apply theme to all controls
            
            _trayIconManager = new TrayIconManager(this.Icon, "Mouse Gesture", RestoreFromTray, Application.Exit);
            
            _gestureEngine = new GestureEngine(_profileManager)
            {
                OnUpdateFeedback = UpdateRealTimeFeedback,
                OnHideFeedback = HideRealTimeFeedback,
                OnHideFeedbackDelayed = (delayMsg) => 
                {
                    // Delay logic
                    var tmr = new Timer { Interval = delayMsg };
                    tmr.Tick += (s, args) => 
                    {
                        tmr.Stop();
                        tmr.Dispose();
                        HideRealTimeFeedback();
                    };
                    tmr.Start();
                },
                IsRecordingGesture = () => _isRecordingGesture,
                IsListeningForKey = () => _isListeningForKey,
                OnCancelEditingMode = () => this.Invoke(new Action(CancelEditingMode)),
                OnGestureRecorded = (gesture) => 
                {
                    this.Invoke(new Action(() => 
                    {
                        if (_editingRowIndex >= 0 && _editingList != null && _editingRowIndex < _editingList.Count)
                        {
                            _editingList[_editingRowIndex].Gesture = gesture;
                            UpdateGestureLabel($"記録完了: {gesture}");
                            
                            if (_editingGrid != null)
                            {
                                foreach (DataGridViewColumn col in _editingGrid.Columns)
                                {
                                    if (col.HeaderText == "ジェスチャー")
                                    {
                                        _editingGrid.Rows[_editingRowIndex].Cells[col.Index].Style.BackColor = _editingGrid.DefaultCellStyle.BackColor;
                                        break;
                                    }
                                }
                                _editingGrid.Refresh();
                            }
                            _isRecordingGesture = false;
                            _editingRowIndex = -1;
                            _editingGrid = null;
                            _editingList = null;
                            _profileManager.SaveGestures();
                        }
                    }));
                }
            };
            _gestureEngine.Start();

            // Feedback Fade Timer
            _feedbackFadeTimer = new Timer();
            _feedbackFadeTimer.Interval = 16; // ~60fps fade
            _feedbackFadeTimer.Tick += (s, args) =>
            {
                if (_feedbackForm == null || _feedbackForm.IsDisposed)
                {
                    _feedbackFadeTimer.Stop();
                    return;
                }

                if (_feedbackForm.Opacity > 0)
                {
                    _feedbackForm.Opacity -= 0.05; // Fade speed
                }
                
                if (_feedbackForm.Opacity <= 0)
                {
                    _feedbackFadeTimer.Stop();
                    _feedbackForm.Hide();
                    _feedbackForm.Opacity = 0.8; // Restore for next use
                }
            };

            // Setup Global Grid
            SetupGridProperties(dgvGestures);
            dgvGestures.DataSource = _profileManager.GlobalGestureMaps;
            
            // Setup App Grid
            SetupGridProperties(_dgvAppGestures);
            _dgvAppGestures.DataSource = _profileManager.CurrentAppGestureMaps;

            // Setup Group Grid
            if (_dgvGroupGestures != null)
            {
                SetupGridProperties(_dgvGroupGestures);
                _dgvGroupGestures.DataSource = _profileManager.CurrentGroupGestureMaps;
            }

            // Handle clicks outside grid to commit edit
            this.MouseDown += EndEditOnMouseDown;
            lblGesture.MouseDown += EndEditOnMouseDown;
            
             // Select first app if available
            if (_cmbApps.Items.Count > 0)
                _cmbApps.SelectedIndex = 0;
        }

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
            panelTop.Height = 70;
            panelTop.BackColor = Color.FromArgb(30, 30, 30);
            
            _lblAppHint = new Label();
            _lblAppHint.Text = "対象アプリ:";
            _lblAppHint.ForeColor = Color.White;
            _lblAppHint.Location = new Point(10, 12);
            _lblAppHint.AutoSize = true;
            
            _cmbApps = new ComboBox();
            _cmbApps.Location = new Point(80, 8);
            _cmbApps.Width = 200;
            _cmbApps.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbApps.SelectedIndexChanged += CmbApps_SelectedIndexChanged;
            
            _btnSelectWindow = new Button();
            _btnSelectWindow.Text = "アプリ追加";
            _btnSelectWindow.Location = new Point(290, 7);
            _btnSelectWindow.Size = new Size(80, 25);
            _btnSelectWindow.FlatStyle = FlatStyle.Flat;
            _btnSelectWindow.BackColor = Color.FromArgb(0, 122, 204);
            _btnSelectWindow.ForeColor = Color.White;
            _btnSelectWindow.Click += BtnSelectWindow_Click;

            _btnRemoveApp = new Button();
            _btnRemoveApp.Text = "削除";
            _btnRemoveApp.Location = new Point(380, 7);
            _btnRemoveApp.Size = new Size(60, 25);
            _btnRemoveApp.FlatStyle = FlatStyle.Flat;
            _btnRemoveApp.BackColor = Color.IndianRed;
            _btnRemoveApp.ForeColor = Color.White;
            _btnRemoveApp.Click += BtnRemoveApp_Click;

            _chkDisableApp = new CheckBox();
            _chkDisableApp.Text = "このアプリを監視しない";
            _chkDisableApp.ForeColor = Color.White;
            _chkDisableApp.Location = new Point(10, 40);
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
            dgv.CellDoubleClick += DgvGestures_CellDoubleClick;
            dgv.CellClick += DgvGestures_CellClick;
            dgv.CellContentClick += DgvGestures_CellContentClick;
            dgv.EditingControlShowing += DgvGestures_EditingControlShowing;
            dgv.MouseDown += EndEditOnMouseDown;

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
                _profileManager.CurrentGroupProfile = null;
                _profileManager.CurrentGroupGestureMaps.Clear();
                _dgvGroupGestures?.Refresh();
            }
        }



        private void BtnRemoveApp_Click(object sender, EventArgs e)
        {
            if (_cmbApps.SelectedItem is AppProfile profile)
            {
                if (MessageBox.Show($"アプリ '{profile.AppName}' の設定を削除しますか？", "確認", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _profileManager.AppProfiles.Remove(profile);
                    RefreshAppList();
                    _profileManager.CurrentAppProfile = null;
                    _profileManager.CurrentAppGestureMaps.Clear();
                    _profileManager.SaveGestures();
                }
            }
        }

        private void CmbApps_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cmbApps.SelectedItem is AppProfile profile)
            {
                // Sync previous
                if (_profileManager.CurrentAppProfile != null)
                {
                     _profileManager.CurrentAppProfile.Gestures = _profileManager.CurrentAppGestureMaps.ToList();
                }

                _profileManager.CurrentAppProfile = profile;
                
                // Update Checkbox without triggering event
                _chkDisableApp.Checked = profile.Disabled;

                _profileManager.CurrentAppGestureMaps.Clear();
                foreach(var g in profile.Gestures) _profileManager.CurrentAppGestureMaps.Add(g);
                
                _dgvAppGestures.Refresh();
            }
        }



        private class OpenAppItem
        {
            public string AppName { get; set; }
            public string AppPath { get; set; }
            public override string ToString() => AppName;
        }

        private void BtnSelectWindow_Click(object sender, EventArgs e)
        {
            using (Form selectAppForm = new Form())
            {
                selectAppForm.Text = "アプリ追加";
                selectAppForm.Size = new Size(400, 180);
                selectAppForm.StartPosition = FormStartPosition.CenterParent;
                selectAppForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                selectAppForm.MaximizeBox = false;

                selectAppForm.BackColor = Color.FromArgb(30, 30, 30);
                selectAppForm.ForeColor = Color.White;

                Label lbl = new Label() { Left = 20, Top = 20, Text = "現在開いているアプリを選択してください:", AutoSize = true, ForeColor = Color.White };
                ComboBox cmbOpenApps = new ComboBox() { Left = 20, Top = 45, Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
                cmbOpenApps.BackColor = Color.FromArgb(45, 45, 48);
                cmbOpenApps.ForeColor = Color.White;
                cmbOpenApps.FlatStyle = FlatStyle.Flat;

                Button btnOk = new Button() { Text = "追加", Left = 200, Width = 80, Top = 90, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White };
                Button btnCancel = new Button() { Text = "キャンセル", Left = 290, Width = 80, Top = 90, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(45, 45, 48), ForeColor = Color.White };
                
                selectAppForm.Controls.Add(lbl);
                selectAppForm.Controls.Add(cmbOpenApps);
                selectAppForm.Controls.Add(btnOk);
                selectAppForm.Controls.Add(btnCancel);
                selectAppForm.AcceptButton = btnOk;
                selectAppForm.CancelButton = btnCancel;

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
                            if (!string.IsNullOrEmpty(path) && !addedPaths.Contains(path) && 
                                !_profileManager.AppProfiles.Any(profile => string.Equals(profile.AppPath, path, StringComparison.OrdinalIgnoreCase)))
                            {
                                addedPaths.Add(path);
                                string exeName = Path.GetFileNameWithoutExtension(path);
                                // Optional logic to uppercase first character if it's all lowercase, typical for names like "chrome"
                                if (exeName.Length > 0 && char.IsLower(exeName[0])) {
                                    exeName = char.ToUpper(exeName[0]) + exeName.Substring(1);
                                }

                                cmbOpenApps.Items.Add(new OpenAppItem 
                                { 
                                    AppName = exeName, 
                                    AppPath = path 
                                });
                            }
                        }
                    }
                    catch { }
                }

                if (cmbOpenApps.Items.Count > 0)
                    cmbOpenApps.SelectedIndex = 0;

                if (selectAppForm.ShowDialog(this) == DialogResult.OK)
                {
                    var selected = cmbOpenApps.SelectedItem as OpenAppItem;
                    if (selected != null)
                    {
                        string path = selected.AppPath;
                        if (_profileManager.AppProfiles.Any(p => p.AppPath != null && p.AppPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                        {
                            MessageBox.Show("このアプリは既に追加されています。");
                        }
                        else
                        {
                            var newProfile = new AppProfile 
                            { 
                                AppPath = path, 
                                AppName = selected.AppName 
                            };
                            _profileManager.AppProfiles.Add(newProfile);
                            RefreshAppList();
                            _cmbApps.SelectedItem = newProfile;
                            UpdateGestureLabel($"アプリ '{newProfile.AppName}' を追加しました。");
                            _profileManager.SaveGestures();
                        }
                    }
                }
            }
        }

        private void EndEditOnMouseDown(object sender, MouseEventArgs e)
        {
            if (dgvGestures.IsCurrentCellInEditMode)
            {
                dgvGestures.EndEdit();
            }
            if (_dgvAppGestures != null && _dgvAppGestures.IsCurrentCellInEditMode)
            {
                _dgvAppGestures.EndEdit();
            }
            if (_dgvGroupGestures != null && _dgvGroupGestures.IsCurrentCellInEditMode)
            {
                _dgvGroupGestures.EndEdit();
            }
        }

        private void DgvGestures_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is TextBox tb)
            {
                tb.BackColor = Color.FromArgb(45, 45, 48);
                tb.ForeColor = Color.White;
                tb.BorderStyle = BorderStyle.FixedSingle;
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

        private void BtnAddGesture_Click(object sender, EventArgs e)
        {
            // Determine which list to add to based on active tab
            if (_tabControl.SelectedTab == _tabGlobal)
            {
                _profileManager.GlobalGestureMaps.Add(new GestureMap());
                if (dgvGestures.Rows.Count > 0)
                {
                    dgvGestures.FirstDisplayedScrollingRowIndex = dgvGestures.Rows.Count - 1;
                }
                UpdateGestureLabel("ジェスチャーフィールドをダブルクリックで追加");
            }
        }

        private void CmbGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cmbGroups.SelectedItem is GroupProfile profile)
            {
                if (_profileManager.CurrentGroupProfile != null)
                {
                     _profileManager.CurrentGroupProfile.Gestures = _profileManager.CurrentGroupGestureMaps.ToList();
                }

                _profileManager.CurrentGroupProfile = profile;
                _profileManager.CurrentGroupGestureMaps.Clear();
                foreach(var g in profile.Gestures) _profileManager.CurrentGroupGestureMaps.Add(g);
                
                _dgvGroupGestures.Refresh();
            }
        }

        private void BtnAddGroup_Click(object sender, EventArgs e)
        {
            using (Form inputForm = new Form())
            {
                inputForm.Text = "グループ作成";
                inputForm.Size = new Size(300, 150);
                inputForm.StartPosition = FormStartPosition.CenterParent;
                inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                inputForm.MaximizeBox = false;

                Label lbl = new Label() { Left = 20, Top = 20, Text = "グループ名:" };
                TextBox txt = new TextBox() { Left = 20, Top = 50, Width = 240 };
                Button btnOk = new Button() { Text = "OK", Left = 180, Width = 80, Top = 80, DialogResult = DialogResult.OK };
                
                inputForm.Controls.Add(lbl);
                inputForm.Controls.Add(txt);
                inputForm.Controls.Add(btnOk);
                inputForm.AcceptButton = btnOk;

                if (inputForm.ShowDialog(this) == DialogResult.OK)
                {
                    string name = txt.Text.Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                         if (_profileManager.GroupProfiles.Any(g => g.GroupName == name)) {
                             MessageBox.Show("同じ名前のグループが既に存在します。");
                             return;
                         }
                         var newGroup = new GroupProfile { GroupName = name };
                         _profileManager.GroupProfiles.Add(newGroup);
                         RefreshGroupList();
                         _cmbGroups.SelectedItem = newGroup;
                         _profileManager.SaveGestures();
                    }
                }
            }
        }

        private void BtnRemoveGroup_Click(object sender, EventArgs e)
        {
             if (_cmbGroups.SelectedItem is GroupProfile profile)
            {
                if (MessageBox.Show($"グループ '{profile.GroupName}' を削除しますか？", "確認", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _profileManager.GroupProfiles.Remove(profile);
                    RefreshGroupList();
                    _profileManager.SaveGestures();
                }
            }
        }

        private void BtnEditGroupApps_Click(object sender, EventArgs e)
        {
             if (_profileManager.CurrentGroupProfile == null) return;

             using (Form listForm = new Form())
             {
                listForm.Text = "アプリ選択";
                listForm.Size = new Size(400, 400);
                listForm.StartPosition = FormStartPosition.CenterParent;
                
                CheckedListBox chkList = new CheckedListBox();
                chkList.Dock = DockStyle.Fill;
                chkList.CheckOnClick = true;

                // List all apps from AppProfiles (except Global)
                var apps = _profileManager.AppProfiles.Where(p => !string.IsNullOrEmpty(p.AppPath)).ToList();
                foreach(var app in apps)
                {
                    bool isChecked = _profileManager.CurrentGroupProfile.TargetAppPaths.Contains(app.AppPath);
                    chkList.Items.Add(app.AppName + " (" + Path.GetFileName(app.AppPath) + ")", isChecked); 
                    // Store AppPath in Tag? Tag is not easily available in Items.Add
                    // Since we iterate in same order, we can rely on index matching for the loop below.
                }

                Panel pnlBottom = new Panel();
                pnlBottom.Dock = DockStyle.Bottom;
                pnlBottom.Height = 50;

                Button btnOk = new Button() { Text = "OK", Dock = DockStyle.Right, Width = 80, DialogResult = DialogResult.OK };
                pnlBottom.Controls.Add(btnOk);

                listForm.Controls.Add(chkList);
                listForm.Controls.Add(pnlBottom);

                if (listForm.ShowDialog(this) == DialogResult.OK)
                {
                    _profileManager.CurrentGroupProfile.TargetAppPaths.Clear();
                    for(int i=0; i<chkList.Items.Count; i++)
                    {
                        if (chkList.GetItemChecked(i))
                        {
                            // Map back to app.
                             var app = apps[i]; // Assuming order is preserved
                             _profileManager.CurrentGroupProfile.TargetAppPaths.Add(app.AppPath);
                        }
                    }
                    _profileManager.SaveGestures();
                }
             }
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



        private bool IsDesignMode()
        {
            return LicenseManager.UsageMode == LicenseUsageMode.Designtime;
        }

        private void DgvGestures_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var dgv = sender as DataGridView;
                var col = dgv.Columns[e.ColumnIndex];
                if (col.Name == "Name" || col.HeaderText == "名前")
                {
                    dgv.BeginEdit(true);
                }
            }
        }

        private void DgvGestures_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var dgv = sender as DataGridView;
                var list = dgv.DataSource as BindingList<GestureMap>;

                if (dgv.Columns[e.ColumnIndex].Name == "Delete")
                {
                    if (e.RowIndex < list.Count)
                    {
                        // Stop editing if active
                        if (_editingRowIndex == e.RowIndex && _editingGrid == dgv)
                        {
                            _isRecordingGesture = false;
                            _isListeningForKey = false;
                            _editingRowIndex = -1;
                            _editingGrid = null;
                            _editingList = null;
                            UpdateGestureLabel("");
                        }
                        
                        list.RemoveAt(e.RowIndex);
                        _profileManager.SaveGestures();
                    }
                }
            }
        }

        private void DgvGestures_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var dgv = sender as DataGridView;
            var list = dgv.DataSource as BindingList<GestureMap>;

            _editingRowIndex = e.RowIndex;
            _editingGrid = dgv;
            _editingList = list;

            if (_editingRowIndex >= list.Count)
            {
                list.Add(new GestureMap());
            }

            string headerText = dgv.Columns[e.ColumnIndex].HeaderText;

            if (headerText == "ジェスチャー") // Gesture
            {
                _isRecordingGesture = true;
                UpdateGestureLabel($"行 {e.RowIndex + 1} のジェスチャーを記録中... (右クリックでキャンセル)");
                dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.DarkRed;
            }
            else if (headerText == "キー割り当て") // KeyBinding
            {
                _isListeningForKey = true;
                UpdateGestureLabel($"行 {e.RowIndex + 1} のキーを押してください... (右クリックでキャンセル)");
                dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.DarkBlue;
            }
        }

        private void CancelEditingMode()
        {
            if (_isRecordingGesture || _isListeningForKey)
            {
                if (_editingGrid != null && _editingRowIndex >= 0 && _editingRowIndex < _editingGrid.Rows.Count)
                {
                    // Restore colors
                    if (_isRecordingGesture)
                    {
                        foreach (DataGridViewColumn col in _editingGrid.Columns)
                        {
                            if (col.HeaderText == "ジェスチャー")
                            {
                                _editingGrid.Rows[_editingRowIndex].Cells[col.Index].Style.BackColor = _editingGrid.DefaultCellStyle.BackColor;
                                break;
                            }
                        }
                    }
                    if (_isListeningForKey)
                    {
                        foreach (DataGridViewColumn col in _editingGrid.Columns)
                        {
                            if (col.HeaderText == "キー割り当て")
                            {
                                _editingGrid.Rows[_editingRowIndex].Cells[col.Index].Style.BackColor = _editingGrid.DefaultCellStyle.BackColor;
                                break;
                            }
                        }
                    }
                    _editingGrid.Refresh();
                }

                _isRecordingGesture = false;
                _isListeningForKey = false;
                _editingRowIndex = -1;
                _editingGrid = null;
                _editingList = null;
                UpdateGestureLabel("キャンセルしました。");
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // ESC key cancellation removed to allow ESC registration and unify on Right-Click cancel

            if (_isListeningForKey && _editingRowIndex >= 0 && _editingList != null && _editingRowIndex < _editingList.Count)
            {
                Keys keyCode = keyData & Keys.KeyCode;
                bool isModifier = keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey || keyCode == Keys.Menu;

                if (!isModifier)
                {
                    string readableFormat = KeyUtils.GetReadableKeyString(keyData);
                    _editingList[_editingRowIndex].KeyBinding = readableFormat;

                    if (string.IsNullOrEmpty(_editingList[_editingRowIndex].Name))
                    {
                        _editingList[_editingRowIndex].Name = readableFormat;
                    }
                    
                    if (_editingGrid != null)
                    {
                        foreach (DataGridViewColumn col in _editingGrid.Columns)
                        {
                            if (col.HeaderText == "キー割り当て")
                            {
                                _editingGrid.Rows[_editingRowIndex].Cells[col.Index].Style.BackColor = _editingGrid.DefaultCellStyle.BackColor;
                                break;
                            }
                        }
                        _editingGrid.Refresh();
                    }
                    
                    _isListeningForKey = false;
                    _editingRowIndex = -1;
                    _editingGrid = null;
                    _editingList = null;
                    UpdateGestureLabel("キーを割り当てました。");
                    _profileManager.SaveGestures();
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }



        private void UpdateRealTimeFeedback(string gesture, Point location)
        {
            if (_isClosing || this.IsDisposed || !this.IsHandleCreated) return;

            if (this.InvokeRequired)
            {
                try
                {
                    this.BeginInvoke(new Action(() => UpdateRealTimeFeedback(gesture, location)));
                }
                catch { }
                return;
            }

            float targetPixelSize = SystemInformation.CursorSize.Height * 0.6f;
            if (targetPixelSize < 12) targetPixelSize = 12;

            if (_feedbackForm == null || _feedbackForm.IsDisposed)
            {
                _feedbackForm = new FeedbackForm();
                _feedbackForm.FormBorderStyle = FormBorderStyle.None;
                _feedbackForm.ShowInTaskbar = false;
                _feedbackForm.StartPosition = FormStartPosition.Manual;
                _feedbackForm.BackColor = Color.FromArgb(30, 30, 30);
                _feedbackForm.Opacity = 0.8;
                _feedbackForm.TopMost = true;
                
                _feedbackLabel = new Label();
                _feedbackLabel.ForeColor = Color.White;
                _feedbackLabel.Font = new Font("Meiryo UI", targetPixelSize, FontStyle.Bold, GraphicsUnit.Pixel);
                _feedbackLabel.AutoSize = true;
                _feedbackLabel.Location = new Point(5, 5);
                
                _feedbackForm.Controls.Add(_feedbackLabel);
            }
            else
            {
                if (Math.Abs(_feedbackLabel.Font.SizeInPoints - (targetPixelSize * 72.0f / _feedbackLabel.DeviceDpi)) > 2.0f && 
                    Math.Abs(_feedbackLabel.Font.Size - targetPixelSize) > 0.5f) 
                {
                     // Size property returns size in Unit of the font. If Unit is Pixel, Size is in Pixels.
                     // However, to be safe, we check if we need update.
                     // Actually, we can just check Size assuming it was created with Pixel unit, 
                     // OR just check if the current font size roughly matches target.
                     // Since we created it with Pixel, Font.Unit should be Pixel.
                     
                     if (_feedbackLabel.Font.Unit == GraphicsUnit.Pixel)
                     {
                         if (Math.Abs(_feedbackLabel.Font.Size - targetPixelSize) > 0.5f)
                         {
                             var oldFont = _feedbackLabel.Font;
                             _feedbackLabel.Font = new Font("Meiryo UI", targetPixelSize, FontStyle.Bold, GraphicsUnit.Pixel);
                             oldFont.Dispose();
                         }
                     }
                     else
                     {
                         // If it was created with Points (legacy), just replace it
                         var oldFont = _feedbackLabel.Font;
                         _feedbackLabel.Font = new Font("Meiryo UI", targetPixelSize, FontStyle.Bold, GraphicsUnit.Pixel);
                         oldFont.Dispose();
                     }
                }
            }

            string displayText = gesture;
            GestureMap match = null;

            if (_isRecordingGesture && _editingList != null)
            {
                match = _editingList.FirstOrDefault(m => m.Gesture == gesture);
            }
            else
            {
                string currentAppPath = ProcessUtils.GetActiveProcessPath();
                match = _profileManager.FindGestureMap(gesture, currentAppPath);
            }

            if (match != null && !string.IsNullOrEmpty(match.Name))
            {
                displayText += " " + match.Name;
            }

            _feedbackLabel.Text = displayText;
            _feedbackForm.Size = new Size(_feedbackLabel.PreferredWidth + 10, _feedbackLabel.PreferredHeight + 10);
            _feedbackForm.Location = new Point(location.X + 20, location.Y + 20);

            if (!_feedbackForm.Visible)
            {
                if (!string.IsNullOrEmpty(displayText))
                {
                    _feedbackFadeTimer?.Stop();
                    _feedbackForm.Opacity = 0.8;
                    _feedbackForm.Show();
                }
            }
            else
            {
                // Ensure opacity is reset if we are just updating text while it's fading (unlikely but possible)
                _feedbackFadeTimer?.Stop();
                _feedbackForm.Opacity = 0.8;
            }
        }

        private void HideRealTimeFeedback()
        {
            if (_isClosing || this.IsDisposed || !this.IsHandleCreated) return;

            if (this.InvokeRequired)
            {
                try
                {
                    this.BeginInvoke(new Action(() => HideRealTimeFeedback()));
                }
                catch { }
                return;
            }

            if (_feedbackForm != null && !_feedbackForm.IsDisposed && _feedbackForm.Visible)
            {
                // Start fade out instead of instant hide
                if (_feedbackFadeTimer != null && !_feedbackFadeTimer.Enabled)
                {
                    _feedbackFadeTimer.Start();
                }
            }
        }

        private void UpdateGestureLabel(string text)
        {
            if (this.IsDisposed) return;

            Action updateAction = () =>
            {
                if (_statusLabel != null)
                {
                    _statusLabel.Text = text;
                }

                // Styling based on content to make it more visible
                if (_statusStrip != null)
                {
                    if (text.Contains("キーを押してください") || 
                        text.Contains("記録中") || text.Contains("ダブルクリック"))
                    {
                        // Action required / Active recording: Orange-Red Background
                        _statusStrip.BackColor = Color.FromArgb(255, 69, 0); 
                        if (_statusLabel != null) _statusLabel.ForeColor = Color.White;
                    }
                    else if (text.Contains("記録完了") || text.Contains("割り当てました"))
                    {
                        // Success: Green Background
                        _statusStrip.BackColor = Color.SeaGreen;
                        if (_statusLabel != null) _statusLabel.ForeColor = Color.White;
                    }
                    else if (string.IsNullOrEmpty(text))
                    {
                        // Default theme color
                        _statusStrip.BackColor = Color.FromArgb(45, 45, 48);
                        if (_statusLabel != null) _statusLabel.ForeColor = Color.White;
                    }
                    else
                    {
                        // Info / Cancel: Dark Background
                        _statusStrip.BackColor = Color.FromArgb(45, 45, 48);
                         if (_statusLabel != null) _statusLabel.ForeColor = Color.White;
                    }
                }
            };

            try
            {
                if (_statusStrip != null && _statusStrip.InvokeRequired)
                    _statusStrip.BeginInvoke(updateAction);
                else
                    updateAction();
            }
            catch (Exception)
            {
                // Ignore errors during disposal
            }
        }

    }


}
