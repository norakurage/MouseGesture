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
        private MouseHook _mouseHook;
        private bool _isRightButtonDown = false;
        private MousePoint _startPoint;
        private List<string> _gestureDirections = new List<string>();
        private const int MOVEMENT_THRESHOLD = 20;
        private const string SETTINGS_FILE = "gestures.xml";
        private const int MAX_GESTURE_STEPS = 8;

        // Data binding
        private BindingList<GestureMap> _gestureMaps = new BindingList<GestureMap>();



        // UI Controls
        private Button _btnAddGesture;
        
        // Real-time feedback
        private Form _feedbackForm;
        private Label _feedbackLabel;

        // Immediate Execution
        private bool _gestureExecuted = false;
        private Timer _feedbackHideTimer;

        // App Profiles
        private List<AppProfile> _appProfiles = new List<AppProfile>();
        private BindingList<GestureMap> _appGestureMaps = new BindingList<GestureMap>();
        private AppProfile _currentAppProfile;

        // UI Controls - Tabs
        private TabControl _tabControl;
        private TabPage _tabGlobal;
        private TabPage _tabApp;
        
        // UI Controls - App Tab
        private ComboBox _cmbApps;
        private Button _btnRemoveApp;
        private DataGridView _dgvAppGestures;
        private Label _lblAppHint;

        // Window Selection
        private Button _btnSelectWindow;
        private bool _isSelectingWindow = false;

        // Editing State
        private int _editingRowIndex = -1;
        private bool _isRecordingGesture = false;
        private bool _isListeningForKey = false;
        private DataGridView _editingGrid; // Track which grid is editing
        private BindingList<GestureMap> _editingList; // Track which list is being edited

        public Form1()

        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (IsDesignMode()) return;

            LoadGestures(); // Load data first

            InitializeCustomUI(); // Create Tabs and move controls
            InitializeAddButton(); // Add buttons
            ApplyModernTheme(); // Apply theme to all controls
            
            _mouseHook = new MouseHook();
            _mouseHook.RightButtonDown += OnRightButtonDown;
            _mouseHook.RightButtonUp += OnRightButtonUp;
            _mouseHook.LeftButtonDown += OnLeftButtonDown;
            _mouseHook.MiddleButtonDown += OnMiddleButtonDown;
            _mouseHook.XButtonDown += OnXButtonDown;
            _mouseHook.MouseMove += OnMouseMove;
            _mouseHook.MouseWheel += OnMouseWheel;
            _mouseHook.Start();

            // Feedback Timer
            _feedbackHideTimer = new Timer();
            _feedbackHideTimer.Interval = 750; // 0.75 seconds delay
            _feedbackHideTimer.Tick += (s, args) => 
            {
                _feedbackHideTimer.Stop();
                HideRealTimeFeedback();
            };

            // Setup Global Grid
            SetupGridProperties(dgvGestures);
            dgvGestures.DataSource = _gestureMaps;
            
            // Setup App Grid
            SetupGridProperties(_dgvAppGestures);
            _dgvAppGestures.DataSource = _appGestureMaps;

            // Handle clicks outside grid to commit edit
            this.MouseDown += EndEditOnMouseDown;
            lblGesture.MouseDown += EndEditOnMouseDown;
            dgvGestures.MouseDown += EndEditOnMouseDown;
            _dgvAppGestures.MouseDown += EndEditOnMouseDown;
            
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
            _tabGlobal.Controls.Add(dgvGestures);
            dgvGestures.Dock = DockStyle.Fill;
            
            // Setup App Tab UI
            Panel panelTop = new Panel();
            panelTop.Dock = DockStyle.Top;
            panelTop.Height = 40;
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

            panelTop.Controls.Add(_lblAppHint);
            panelTop.Controls.Add(_cmbApps);
            panelTop.Controls.Add(_btnSelectWindow);
            panelTop.Controls.Add(_btnRemoveApp);
            
            _dgvAppGestures = new DataGridView();
            _dgvAppGestures.Dock = DockStyle.Fill;
            _dgvAppGestures.BackgroundColor = Color.FromArgb(30, 30, 30);
            
            _tabApp.Controls.Add(_dgvAppGestures);
            _tabApp.Controls.Add(panelTop);
            
            _tabControl.Controls.Add(_tabGlobal);
            _tabControl.Controls.Add(_tabApp);
            
            this.Controls.Add(_tabControl);
            
            // Adjust Label Logic
            lblGesture.Parent = this; // Ensure it stays on top or outside tabs
            lblGesture.BringToFront();
            
            // Populate App Combo
            RefreshAppList();
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


        private void LoadGestures()
        {
            try
            {
                if (File.Exists(SETTINGS_FILE))
                {
                    string rootName = "";
                    using (System.Xml.XmlReader reader = System.Xml.XmlReader.Create(SETTINGS_FILE))
                    {
                        if (reader.MoveToContent() == System.Xml.XmlNodeType.Element)
                        {
                            rootName = reader.Name;
                        }
                    }

                    if (rootName == "ArrayOfAppProfile")
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(List<AppProfile>));
                        using (FileStream fs = new FileStream(SETTINGS_FILE, FileMode.Open))
                        {
                            _appProfiles = (List<AppProfile>)serializer.Deserialize(fs);
                        }
                    }
                    else
                    {
                        // Fallback: Try loading old format (List<GestureMap>) or unknown
                        // This handles "ArrayOfGestureMap"
                        XmlSerializer serializer = new XmlSerializer(typeof(List<GestureMap>));
                        using (FileStream fs = new FileStream(SETTINGS_FILE, FileMode.Open))
                        {
                            var loadedGestures = (List<GestureMap>)serializer.Deserialize(fs);
                            _appProfiles.Clear();
                            _appProfiles.Add(new AppProfile { AppName = "Global", Gestures = loadedGestures });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定の読み込みに失敗しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Ensure Global profile exists
            if (_appProfiles.Count == 0 || !_appProfiles.Any(p => string.IsNullOrEmpty(p.AppPath)))
            {
                var global = _appProfiles.FirstOrDefault(p => string.IsNullOrEmpty(p.AppPath));
                if (global == null)
                {
                    global = new AppProfile { AppName = "Global", AppPath = "" };
                    _appProfiles.Insert(0, global);
                }
            }

            // Populate Main Global Binding List
            var globalProfile = _appProfiles.First(p => string.IsNullOrEmpty(p.AppPath));
            _gestureMaps.Clear();
            foreach(var g in globalProfile.Gestures) _gestureMaps.Add(g);
        }

        private void SaveGestures()
        {
            try
            {
                // Sync Global
                var globalProfile = _appProfiles.First(p => string.IsNullOrEmpty(p.AppPath));
                globalProfile.Gestures = _gestureMaps.ToList();

                // Sync Current App (if selected)
                if (_currentAppProfile != null)
                {
                    _currentAppProfile.Gestures = _appGestureMaps.ToList();
                }

                XmlSerializer serializer = new XmlSerializer(typeof(List<AppProfile>));
                using (FileStream fs = new FileStream(SETTINGS_FILE, FileMode.Create))
                {
                    serializer.Serialize(fs, _appProfiles);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定の保存に失敗しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshAppList()
        {
            _cmbApps.Items.Clear();
            foreach (var app in _appProfiles.Where(a => !string.IsNullOrEmpty(a.AppPath)))
            {
                _cmbApps.Items.Add(app);
            }
            _cmbApps.DisplayMember = "AppName";
        }



        private void BtnRemoveApp_Click(object sender, EventArgs e)
        {
            if (_cmbApps.SelectedItem is AppProfile profile)
            {
                if (MessageBox.Show($"アプリ '{profile.AppName}' の設定を削除しますか？", "確認", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _appProfiles.Remove(profile);
                    RefreshAppList();
                    _currentAppProfile = null;
                    _appGestureMaps.Clear();
                }
            }
        }

        private void CmbApps_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cmbApps.SelectedItem is AppProfile profile)
            {
                // Save previous if needed? We use BindingList so it should be fine if we sync before save.
                // But we need to sync back to the object when switching AWAY? 
                // Actually the current object IS the one in the list.
                // But _appGestureMaps is a separate BindingList. 
                
                // Sync previous
                if (_currentAppProfile != null)
                {
                     _currentAppProfile.Gestures = _appGestureMaps.ToList();
                }

                _currentAppProfile = profile;
                _appGestureMaps.Clear();
                foreach(var g in profile.Gestures) _appGestureMaps.Add(g);
                
                _dgvAppGestures.Refresh();
            }
        }

        private void BtnSelectWindow_Click(object sender, EventArgs e)
        {
            _isSelectingWindow = !_isSelectingWindow;
            if (_isSelectingWindow)
            {
                UpdateGestureLabel("このウィンドウ以外の対象ウィンドウをクリックしてください...");
                _btnSelectWindow.BackColor = Color.OrangeRed;
            }
            else
            {
                UpdateGestureLabel("");
                _btnSelectWindow.BackColor = Color.FromArgb(0, 122, 204);
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
            _btnAddGesture.Text = "追加";
            _btnAddGesture.Size = new Size(100, 35);
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
            _btnAddGesture.Location = new Point(_tabGlobal.Width - 120, _tabGlobal.Height - 50);
            _tabGlobal.Controls.Add(_btnAddGesture);
            _btnAddGesture.BringToFront();

            // Add Button (App) - Create a separate instance or reuse?
            // Reusing might be tricky if tabs switch. Better create another button for App tab.
            Button btnAddAppGesture = new Button();
            btnAddAppGesture.Text = "追加";
            btnAddAppGesture.Size = new Size(100, 35);
            btnAddAppGesture.FlatStyle = FlatStyle.Flat;
            btnAddAppGesture.FlatAppearance.BorderSize = 0;
            btnAddAppGesture.BackColor = Color.FromArgb(0, 122, 204);
            btnAddAppGesture.ForeColor = Color.White;
            btnAddAppGesture.Font = new Font("Meiryo UI", 10, FontStyle.Bold);
            btnAddAppGesture.Cursor = Cursors.Hand;
            btnAddAppGesture.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnAddAppGesture.Click += (s, e) => 
            {
                 if (_currentAppProfile == null) 
                 {
                     MessageBox.Show("アプリを選択してください。");
                     return;
                 }
                 _appGestureMaps.Add(new GestureMap());
                 if (_dgvAppGestures.Rows.Count > 0)
                     _dgvAppGestures.FirstDisplayedScrollingRowIndex = _dgvAppGestures.Rows.Count - 1;
            };
            
            // Layout is not finalized yet in Initialize, so place it anchored
            btnAddAppGesture.Location = new Point(400, 300); // Temporary, Anchor will handle it
            _tabApp.Controls.Add(btnAddAppGesture);
            btnAddAppGesture.BringToFront();
        }

        private void BtnAddGesture_Click(object sender, EventArgs e)
        {
            // Determine which list to add to based on active tab
            if (_tabControl.SelectedTab == _tabGlobal)
            {
                _gestureMaps.Add(new GestureMap());
                if (dgvGestures.Rows.Count > 0)
                {
                    dgvGestures.FirstDisplayedScrollingRowIndex = dgvGestures.Rows.Count - 1;
                }
            }
            else
            {
                // Should be handled by the other button, but just in case
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

            // Label
            lblGesture.ForeColor = Color.White;
            
            // Adjust buttons if needed
             if (_btnAddGesture != null && _tabGlobal != null)
            {
                 _btnAddGesture.Location = new Point(_tabGlobal.Width - 120, _tabGlobal.Height - 50);
            }
        }

        private void StyleTextBox(TextBox txt)
        {
            txt.BackColor = Color.FromArgb(45, 45, 48);
            txt.ForeColor = Color.White;
            txt.BorderStyle = BorderStyle.FixedSingle;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveGestures();
            _mouseHook?.Stop();
            base.OnFormClosing(e);
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
                UpdateGestureLabel($"行 {e.RowIndex + 1} のジェスチャーを記録中...");
                dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.DarkRed;
            }
            else if (headerText == "キー割り当て") // KeyBinding
            {
                _isListeningForKey = true;
                UpdateGestureLabel($"行 {e.RowIndex + 1} のキーを押してください...");
                dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.DarkBlue;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_isListeningForKey && _editingRowIndex >= 0 && _editingList != null && _editingRowIndex < _editingList.Count)
            {
                Keys keyCode = keyData & Keys.KeyCode;
                bool isModifier = keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey || keyCode == Keys.Menu;

                if (!isModifier)
                {
                    string readableFormat = GetReadableKeyString(keyData);
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
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private string GetReadableKeyString(Keys keyData)
        {
            List<string> parts = new List<string>();
            if ((keyData & Keys.Control) == Keys.Control) parts.Add("Ctrl");
            if ((keyData & Keys.Alt) == Keys.Alt) parts.Add("Alt");
            if ((keyData & Keys.Shift) == Keys.Shift) parts.Add("Shift");

            Keys keyCode = keyData & Keys.KeyCode;
            if (keyCode != Keys.ControlKey && keyCode != Keys.ShiftKey && keyCode != Keys.Menu && keyCode != Keys.None)
            {
                if (keyCode >= Keys.D0 && keyCode <= Keys.D9)
                {
                    parts.Add(keyCode.ToString().Replace("D", ""));
                }
                else
                {
                    parts.Add(keyCode.ToString());
                }
            }
            
            return string.Join("+", parts);
        }

        private string ConvertReadableToSendKeys(string readable)
        {
            if (string.IsNullOrEmpty(readable)) return "";
            
            if (!readable.Contains("Ctrl") && !readable.Contains("Alt") && !readable.Contains("Shift") && 
                (readable.Contains("^") || readable.Contains("%") || readable.Contains("{")))
            {
                return readable;
            }

            string result = "";
            
            var parts = readable.Split('+').ToList();
            bool ctrl = parts.Remove("Ctrl");
            bool alt = parts.Remove("Alt");
            bool shift = parts.Remove("Shift");
            
            if (ctrl) result += "^";
            if (alt) result += "%";
            if (shift) result += "+";
            
            if (parts.Count > 0)
            {
                string key = parts.Last(); 
                
                if (key.Length == 1)
                {
                    result += key.ToLower();
                }
                else
                {
                    switch (key.ToUpper())
                    {
                        case "ENTER": result += "{ENTER}"; break;
                        case "RETURN": result += "{ENTER}"; break;
                        case "TAB": result += "{TAB}"; break;
                        case "ESCAPE": result += "{ESC}"; break;
                        case "ESC": result += "{ESC}"; break;
                        case "BACK": result += "{BACKSPACE}"; break;
                        case "BACKSPACE": result += "{BACKSPACE}"; break;
                        case "DELETE": result += "{DELETE}"; break;
                        default: result += "{" + key.ToUpper() + "}"; break;
                    }
                }
            }
            
            return result;
        }

        private void OnRightButtonDown(object sender, MouseHookEventArgs e)
        {
            _isRightButtonDown = true;
            _gestureExecuted = false;
            _startPoint = e.Point;
            _gestureDirections.Clear();
            
            _gestureDirections.Clear();

            // Reset feedback state for new gesture
            _feedbackHideTimer?.Stop();
            HideRealTimeFeedback();
            
            UpdateRealTimeFeedback("", new Point(e.Point.X, e.Point.Y));
            
            e.Handled = true;
        }

        private void OnRightButtonUp(object sender, MouseHookEventArgs e)
        {
            _isRightButtonDown = false;
            
            // Only hide feedback if we're not waiting for an immediate gesture feedback to expire
            if (_feedbackHideTimer == null || !_feedbackHideTimer.Enabled)
            {
                HideRealTimeFeedback();
            }
            
            string finalGesture = string.Join("", _gestureDirections);
            e.Handled = true;

            if (_isRecordingGesture && _editingRowIndex >= 0 && _editingList != null && _editingRowIndex < _editingList.Count)
            {
                if (!string.IsNullOrEmpty(finalGesture))
                {
                    _editingList[_editingRowIndex].Gesture = finalGesture;
                    UpdateGestureLabel($"記録完了: {finalGesture}");
                }
                else
                {
                    UpdateGestureLabel("記録キャンセル");
                }
                
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
            }
            else
            {
                if (_isListeningForKey) return;

                if (_gestureExecuted)
                {
                    // Do nothing if already executed
                }
                else if (!string.IsNullOrEmpty(finalGesture))
                {
                    // UpdateGestureLabel($"ジェスチャー: {finalGesture}");
                    ExecuteGesture(finalGesture);
                }
                else
                {
                    SendRightClick(e.Point);
                    // UpdateGestureLabel("右クリック");
                }
            }
        }

        private string GetActiveProcessPath()
        {
            try
            {
                IntPtr hWnd = NativeMethods.GetForegroundWindow();
                if (hWnd == IntPtr.Zero) return null;

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
                
                IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        StringBuilder buffer = new StringBuilder(1024);
                        uint size = (uint)buffer.Capacity;
                        if (NativeMethods.QueryFullProcessImageName(hProcess, 0, buffer, ref size))
                        {
                            return buffer.ToString();
                        }
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(hProcess);
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void SendRightClick(MousePoint p)
        {
            // Perform click asynchronously to avoid blocking the hook and to add delay
            Task.Run(async () => 
            {
                var inputDown = new MouseHook.INPUT
                {
                    type = MouseHook.INPUT_MOUSE,
                    U = new MouseHook.InputUnion
                    {
                        mi = new MouseHook.MOUSEINPUT
                        {
                            dwFlags = MouseHook.MOUSEEVENTF_RIGHTDOWN,
                            dwExtraInfo = MouseHook.INJECTED_EVENT_ID
                        }
                    }
                };

                MouseHook.SendInput(1, new[] { inputDown }, MouseHook.INPUT.Size);

                await Task.Delay(50); 

                var inputUp = new MouseHook.INPUT
                {
                    type = MouseHook.INPUT_MOUSE,
                    U = new MouseHook.InputUnion
                    {
                        mi = new MouseHook.MOUSEINPUT
                        {
                            dwFlags = MouseHook.MOUSEEVENTF_RIGHTUP,
                            dwExtraInfo = MouseHook.INJECTED_EVENT_ID
                        }
                    }
                };

                MouseHook.SendInput(1, new[] { inputUp }, MouseHook.INPUT.Size);
            });
        }

        private void OnMouseMove(object sender, MouseHookEventArgs e)
        {
            if (!_isRightButtonDown) return;

            int dx = e.Point.X - _startPoint.X;
            int dy = e.Point.Y - _startPoint.Y;

            if (Math.Abs(dx) > MOVEMENT_THRESHOLD || Math.Abs(dy) > MOVEMENT_THRESHOLD)
            {
                string direction = "";
                if (Math.Abs(dx) > Math.Abs(dy))
                    direction = dx > 0 ? "→" : "←";
                else
                    direction = dy > 0 ? "↓" : "↑";

                if (_gestureDirections.Count < MAX_GESTURE_STEPS && (_gestureDirections.Count == 0 || _gestureDirections.Last() != direction))
                {
                    _gestureDirections.Add(direction);
                    // UpdateGestureLabel(string.Join(" -> ", _gestureDirections));
                    _startPoint = e.Point;
                }
                else if (_gestureDirections.Count > 0 && _gestureDirections.Last() == direction)
                {
                    _startPoint = e.Point;
                }
            }
            else
            {
                if (_gestureDirections.Count > 0)
                {
                    string last = _gestureDirections.Last();
                    if ((last == "→" && dx > 0) ||
                        (last == "←" && dx < 0) ||
                        (last == "↓" && dy > 0) ||
                        (last == "↑" && dy < 0))
                    {
                        _startPoint = e.Point;
                    }
                }
            }
            
            string currentGesture = string.Join("", _gestureDirections);
            UpdateRealTimeFeedback(currentGesture, new Point(e.Point.X, e.Point.Y));

            // Immediate execution logic removed for gestures
            /*
            var map = _gestureMaps.FirstOrDefault(m => m.Gesture == currentGesture);
            if (map != null)
            {
                ExecuteGesture(currentGesture);
                _gestureExecuted = true;
                _gestureDirections.Clear();
                _startPoint = e.Point; 
                UpdateGestureLabel($"即時実行: {currentGesture}");
                HideRealTimeFeedback();
            }
            */
        }

        private void OnMouseWheel(object sender, MouseHookEventArgs e)
        {
            if (!_isRightButtonDown) return;

            if (_gestureDirections.Count >= MAX_GESTURE_STEPS) return;

            short delta = (short)((e.MouseData >> 16) & 0xFFFF);
            if (delta == 0) return;

            string direction = delta > 0 ? "⇑" : "⇓";

            if (_gestureDirections.Count == 0 || _gestureDirections.Last() != direction)
            {
                _gestureDirections.Add(direction);
                string currentGesture = string.Join("", _gestureDirections);
                // UpdateGestureLabel(string.Join(" -> ", _gestureDirections));
                UpdateRealTimeFeedback(currentGesture, new Point(e.Point.X, e.Point.Y));

                // Immediate execution logic
                if (_isRecordingGesture || _isListeningForKey) return;

                var map = FindGestureMap(currentGesture);
                if (map != null)
                {
                    ExecuteGesture(currentGesture);
                    _gestureExecuted = true;
                    _gestureDirections.Clear();
                    // UpdateGestureLabel($"即時実行: {currentGesture}");
                    
                    // Show feedback for a while then hide
                    _feedbackHideTimer.Stop();
                    _feedbackHideTimer.Start();
                }
            }
            
            e.Handled = true;
        }

        private void OnLeftButtonDown(object sender, MouseHookEventArgs e)
        {
            if (_isSelectingWindow)
            {
                // Handle Window Selection
                IntPtr hWnd = NativeMethods.WindowFromPoint(new Point(e.Point.X, e.Point.Y));
                if (hWnd != IntPtr.Zero)
                {
                    // Get root window or foreground? Usually need PID.
                    NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
                    
                    IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                    if (hProcess != IntPtr.Zero)
                    {
                        try
                        {
                            StringBuilder buffer = new StringBuilder(1024);
                            uint size = (uint)buffer.Capacity;
                            if (NativeMethods.QueryFullProcessImageName(hProcess, 0, buffer, ref size))
                            {
                                string path = buffer.ToString();
                                
                                // Check if it's our own app
                                int currentPid = Process.GetCurrentProcess().Id;
                                if (processId == currentPid)
                                {
                                    // Ignore clicks on self for selection (or maybe allow?)
                                    // Usually better to ignore or handle gracefully
                                }
                                else
                                {
                                    this.Invoke(new Action(() => 
                                    {
                                        if (_appProfiles.Any(p => p.AppPath != null && p.AppPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            MessageBox.Show("このアプリは既に追加されています。");
                                        }
                                        else
                                        {
                                            var newProfile = new AppProfile 
                                            { 
                                                AppPath = path, 
                                                AppName = Path.GetFileNameWithoutExtension(path) 
                                            };
                                            _appProfiles.Add(newProfile);
                                            RefreshAppList();
                                            _cmbApps.SelectedItem = newProfile;
                                            UpdateGestureLabel($"アプリ '{newProfile.AppName}' を追加しました。");
                                        }
                                        
                                        // Reset State
                                        _isSelectingWindow = false;
                                        _btnSelectWindow.BackColor = Color.FromArgb(0, 122, 204);
                                    }));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            this.Invoke(new Action(() => MessageBox.Show($"アプリ情報の取得に失敗しました: {ex.Message}")));
                        }
                        finally
                        {
                            NativeMethods.CloseHandle(hProcess);
                        }
                    }
                }
                
                e.Handled = true; // Consume the click
                return;
            }

            if (!_isRightButtonDown) return;
            AddClickGesture("L", e);
        }

        private void OnMiddleButtonDown(object sender, MouseHookEventArgs e)
        {
            if (!_isRightButtonDown) return;
            AddClickGesture("M", e);
        }

        private void OnXButtonDown(object sender, MouseHookEventArgs e)
        {
            if (!_isRightButtonDown) return;
            
            int xButton = (e.MouseData >> 16) & 0xFFFF;
            string direction = xButton == 1 ? "X1" : "X2";
            AddClickGesture(direction, e);
        }

        private void AddClickGesture(string direction, MouseHookEventArgs e)
        {
            if (_gestureDirections.Count >= MAX_GESTURE_STEPS) return;

            _gestureDirections.Add(direction);
            
            string currentGesture = string.Join("", _gestureDirections);
            // UpdateGestureLabel(string.Join(" -> ", _gestureDirections));
            UpdateRealTimeFeedback(currentGesture, new Point(e.Point.X, e.Point.Y));
            
            e.Handled = true;

            // Immediate execution logic (Default now)
            // Skip execution if recording or listening for key
            if (_isRecordingGesture || _isListeningForKey) return;

            GestureMap map = null;
            if (!_isRecordingGesture)
            {
                 map = FindGestureMap(currentGesture);
            }

            if (map != null)
            {
                ExecuteGesture(currentGesture);
                _gestureExecuted = true;
                _gestureDirections.Clear();
                _startPoint = e.Point; // Reset start point for potential future moves
                // UpdateGestureLabel($"即時実行: {currentGesture}");
                
                // Show feedback for a while then hide
                _feedbackHideTimer.Stop();
                _feedbackHideTimer.Start();
            }
        }

        private void UpdateRealTimeFeedback(string gesture, Point location)
        {
            float targetPixelSize = SystemInformation.CursorSize.Height * 0.6f;
            if (targetPixelSize < 12) targetPixelSize = 12;

            if (_feedbackForm == null || _feedbackForm.IsDisposed)
            {
                _feedbackForm = new Form();
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
                match = FindGestureMap(gesture);
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
                    _feedbackForm.Show();
            }
        }

        private void HideRealTimeFeedback()
        {
            if (_feedbackForm != null && !_feedbackForm.IsDisposed)
            {
                _feedbackForm.Hide();
            }
        }

        private void UpdateGestureLabel(string text)
        {
            if (this.IsDisposed) return;

            Action updateAction = () =>
            {
                lblGesture.Text = text;

                // Styling based on content to make it more visible
                if (text.Contains("キーを押してください") || 
                    text.Contains("記録中"))
                {
                    // Action required / Active recording: Orange-Red Background
                    lblGesture.BackColor = Color.FromArgb(255, 69, 0); 
                    lblGesture.ForeColor = Color.White;
                }
                else if (text.Contains("記録完了") || text.Contains("割り当てました"))
                {
                    // Success: Green Background
                    lblGesture.BackColor = Color.SeaGreen;
                    lblGesture.ForeColor = Color.White;
                }
                else if (string.IsNullOrEmpty(text))
                {
                    lblGesture.BackColor = Color.Transparent;
                }
                else
                {
                    // Info / Cancel: Dark Background
                    lblGesture.BackColor = Color.FromArgb(45, 45, 48);
                    lblGesture.ForeColor = Color.White;
                }

                if (!string.IsNullOrEmpty(text))
                {
                    // Center the label on the form
                    lblGesture.AutoSize = true;
                    // Force layout measurement
                    Size preferredSize = lblGesture.GetPreferredSize(new Size(this.ClientSize.Width, 0));
                    
                    int x = (this.ClientSize.Width - preferredSize.Width) / 2;
                    int y = (this.ClientSize.Height - preferredSize.Height) / 2;
                    
                    // Show slightly above center for better visibility
                    y -= 20; 
                    if (y < 0) y = 0;
                    
                    lblGesture.Location = new Point(x, y);
                    lblGesture.Visible = true;
                    lblGesture.BringToFront();
                }
                else
                {
                    lblGesture.Visible = false;
                }
            };

            try
            {
                if (lblGesture.InvokeRequired)
                    lblGesture.BeginInvoke(updateAction);
                else
                    updateAction();
            }
            catch (Exception)
            {
                // Ignore errors during disposal
            }
        }

        private GestureMap FindGestureMap(string gesture)
        {
            string appPath = GetActiveProcessPath();
            AppProfile appProfile = null;

            if (!string.IsNullOrEmpty(appPath))
            {
                // 1. Try exact path match
                appProfile = _appProfiles.FirstOrDefault(p => !string.IsNullOrEmpty(p.AppPath) && appPath.Equals(p.AppPath, StringComparison.OrdinalIgnoreCase));

                // 2. Try file name match (Process name match)
                if (appProfile == null)
                {
                    try
                    {
                        string appName = Path.GetFileName(appPath);
                        appProfile = _appProfiles.FirstOrDefault(p => !string.IsNullOrEmpty(p.AppPath) && 
                            string.Equals(Path.GetFileName(p.AppPath), appName, StringComparison.OrdinalIgnoreCase));
                    }
                    catch { }
                }
            }

            GestureMap map = null;
            if (appProfile != null)
            {
                map = appProfile.Gestures.FirstOrDefault(m => m.Gesture == gesture);
            }

            // Fallback to Global
            if (map == null)
            {
                var globalProfile = _appProfiles.FirstOrDefault(p => string.IsNullOrEmpty(p.AppPath));
                if (globalProfile != null)
                {
                    map = globalProfile.Gestures.FirstOrDefault(m => m.Gesture == gesture);
                }
            }

            return map;
        }

        private void ExecuteGesture(string gesture)
        {
            GestureMap map = FindGestureMap(gesture);

            if (map != null)
            {
                if (!string.IsNullOrEmpty(map.KeyBinding))
                {
                    // Use BeginInvoke to send keys asynchronously, avoiding conflicts with the hook procedure
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            string sendKeys = ConvertReadableToSendKeys(map.KeyBinding);
                            SendKeys.Send(sendKeys);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error sending keys: {ex.Message}");
                        }
                    }));
                }
            }
        }
    }


}
