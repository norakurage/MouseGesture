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
        private string SettingsFilePath => Path.Combine(AppContext.BaseDirectory, "gestures.xml");

        // 新しいコンポーネント
        private ProfileManager _profileManager;
        private TrayIconManager _trayIconManager;

        // UI Controls
        private Button _btnAddGesture;
        private Button _btnAddAppGesture;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;
        
        // Real-time feedback
        private UI.FeedbackOverlay _feedbackOverlay;

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
        private GestureEditController _gestureEditController;
        private volatile bool _isClosing = false;

        // System Tray
        
        // Auto Start
        private CheckBox _chkAutoStart;

        // シングルインスタンス制御
        private System.Threading.EventWaitHandle _showWindowWaitHandle;
        private System.Threading.RegisteredWaitHandle _registeredWaitHandle;

        private bool _isAutoStart;

        public Form1(string[] args = null)
        {
            if (args != null)
            {
                _isAutoStart = args.Any(a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase))
                    || Environment.GetEnvironmentVariable("APP_AUTOSTART") == "1";
            }

            InitializeComponent();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                if (_isAutoStart)
                {
                    cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                }
                return cp;
            }
        }

        private bool _isInitialized = false;

        protected override void OnLoad(EventArgs e)
        {
            try
            {
                if (_isAutoStart)
                {
                    this.Visible = false;
                    this.ShowInTaskbar = false;
                }

                base.OnLoad(e);
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "load_crash.log"), ex.ToString());
                throw;
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            if (_isAutoStart)
            {
                value = false;
                if (!this.IsHandleCreated) 
                {
                    CreateHandle();
                    Form1_Load(this, EventArgs.Empty);
                }
            }

            base.SetVisibleCore(value);
        }

        private void RestoreFromTray()
        {
            _isAutoStart = false;
            this.ShowInTaskbar = true;
            this.Visible = true;
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
                _gestureEditController?.StopEngine();
                
                _registeredWaitHandle?.Unregister(null);
                _showWindowWaitHandle?.Dispose();

                _profileManager?.SaveGestures();
                _trayIconManager?.Dispose();
                _gestureEditController?.Dispose();
                _feedbackOverlay?.Dispose();

                base.OnFormClosing(e);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (_isInitialized) return;
            _isInitialized = true;
            try {
                if (IsDesignMode()) return;

                // シングルインスタンス用のイベント待機ハンドル
                _showWindowWaitHandle = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, "MouseGestureApp_ShowWindowEvent");
                _registeredWaitHandle = System.Threading.ThreadPool.RegisterWaitForSingleObject(_showWindowWaitHandle, ShowWindowCallback, null, -1, false);

                _profileManager = new ProfileManager(SettingsFilePath);
                _profileManager.LoadGestures();

                _gestureEditController = new GestureEditController(_profileManager, UpdateGestureLabel);

                this.KeyPreview = true;
                this.KeyDown += Form1_KeyDown;

                InitializeCustomUI(); // Create Tabs and move controls
                InitializeAddButton(); // Add buttons
                ApplyModernTheme(); // Apply theme to all controls
                
                _trayIconManager = new TrayIconManager(this.Icon, "Mouse Gesture", RestoreFromTray, () => this.BeginInvoke(new Action(this.Close)));
                
                _gestureEditController.InitializeEngine(
                    UpdateRealTimeFeedback,
                    HideRealTimeFeedback,
                    (delayMsg) => 
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
                    (action) => this.Invoke(action)
                );
                _gestureEditController.StartEngine();

                // Initialize Feedback Overlay
                _feedbackOverlay = new UI.FeedbackOverlay();

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
                this.MouseDown += _gestureEditController.HandleMouseDownOnOtherControls;
                lblGesture.MouseDown += _gestureEditController.HandleMouseDownOnOtherControls;
                
                // Select first app if available
                if (_cmbApps.Items.Count > 0)
                    _cmbApps.SelectedIndex = 0;
            } catch (Exception) {
                // Ignore errors during disposal
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
                    _profileManager.SelectAppProfile(null);
                    _profileManager.SaveGestures();
                }
            }
        }

        private void CmbApps_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cmbApps.SelectedItem is AppProfile profile)
            {
                _profileManager.SelectAppProfile(profile);
                
                // Update Checkbox without triggering event
                _chkDisableApp.Checked = profile.Disabled;

                _dgvAppGestures.Refresh();
            }
        }



        private void BtnSelectWindow_Click(object sender, EventArgs e)
        {
            var existingPaths = _profileManager.AppProfiles
                .Where(p => !string.IsNullOrEmpty(p.AppPath))
                .Select(p => p.AppPath);

            using (var dlg = new UI.OpenAppSelectorDialog(existingPaths))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var newProfile = new AppProfile 
                    { 
                        AppPath = dlg.SelectedAppPath, 
                        AppName = dlg.SelectedAppName 
                    };
                    _profileManager.AppProfiles.Add(newProfile);
                    RefreshAppList();
                    _cmbApps.SelectedItem = newProfile;
                    UpdateGestureLabel($"アプリ '{newProfile.AppName}' を追加しました。");
                    _profileManager.SaveGestures();
                }
            }
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
                _profileManager.SelectGroupProfile(profile);
                _dgvGroupGestures.Refresh();
            }
        }

        private void BtnAddGroup_Click(object sender, EventArgs e)
        {
            var existingNames = _profileManager.GroupProfiles.Select(g => g.GroupName);
            string newName = UI.GroupEditorDialogs.ShowCreateGroupDialog(this, existingNames);
            
            if (!string.IsNullOrEmpty(newName))
            {
                var newGroup = new GroupProfile { GroupName = newName };
                _profileManager.GroupProfiles.Add(newGroup);
                RefreshGroupList();
                _cmbGroups.SelectedItem = newGroup;
                _profileManager.SaveGestures();
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

             var apps = _profileManager.AppProfiles.Where(p => !string.IsNullOrEmpty(p.AppPath));
             if (UI.GroupEditorDialogs.ShowAppSelectionDialog(this, _profileManager.CurrentGroupProfile, apps))
             {
                 _profileManager.SaveGestures();
             }
        }





        private bool IsDesignMode()
        {
            return LicenseManager.UsageMode == LicenseUsageMode.Designtime;
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (_gestureEditController != null && _gestureEditController.HandleKeyDown(e.KeyData))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
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

            string displayText = gesture;
            GestureMap match = null;

            if (_gestureEditController != null && _gestureEditController.IsRecordingGesture)
            {
                // 編集中は登録済みのジェスチャー名を検索しない
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

            _feedbackOverlay?.ShowGesture(displayText, location);
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

            _feedbackOverlay?.Hide();
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
