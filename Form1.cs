using System;
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

        // Editing state
        private int _editingRowIndex = -1;
        private bool _isRecordingGesture = false;
        private bool _isListeningForKey = false;

        // UI Controls
        private Button _btnAddGesture;
        
        // Real-time feedback
        private Form _feedbackForm;
        private Label _feedbackLabel;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (IsDesignMode()) return;

            ApplyModernTheme();
            InitializeAddButton();
            LoadGestures();

            _mouseHook = new MouseHook();
            _mouseHook.RightButtonDown += OnRightButtonDown;
            _mouseHook.RightButtonUp += OnRightButtonUp;
            _mouseHook.LeftButtonDown += OnLeftButtonDown;
            _mouseHook.MiddleButtonDown += OnMiddleButtonDown;
            _mouseHook.XButtonDown += OnXButtonDown;
            _mouseHook.MouseMove += OnMouseMove;
            _mouseHook.MouseWheel += OnMouseWheel;
            _mouseHook.Start();

            // Setup Grid
            dgvGestures.AllowUserToAddRows = false;
            dgvGestures.DataSource = _gestureMaps;
            dgvGestures.CellDoubleClick += DgvGestures_CellDoubleClick;
            dgvGestures.CellClick += DgvGestures_CellClick;
            dgvGestures.EditingControlShowing += DgvGestures_EditingControlShowing;

            // Configure Columns
            // Ensure columns are generated
            if (dgvGestures.Columns.Count > 0)
            {
                if (dgvGestures.Columns["Name"] != null) dgvGestures.Columns["Name"].ReadOnly = false;
                if (dgvGestures.Columns["Gesture"] != null) dgvGestures.Columns["Gesture"].ReadOnly = true;
                if (dgvGestures.Columns["KeyBinding"] != null) dgvGestures.Columns["KeyBinding"].ReadOnly = true;
            }

            // Resize grid to make room for the button
            dgvGestures.Height -= 50;

            // Handle clicks outside grid to commit edit
            this.MouseDown += EndEditOnMouseDown;
            lblGesture.MouseDown += EndEditOnMouseDown;
            dgvGestures.MouseDown += EndEditOnMouseDown;
        }

        private void LoadGestures()
        {
            try
            {
                if (File.Exists(SETTINGS_FILE))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<GestureMap>));
                    using (FileStream fs = new FileStream(SETTINGS_FILE, FileMode.Open))
                    {
                        var loadedGestures = (List<GestureMap>)serializer.Deserialize(fs);
                        _gestureMaps.Clear();
                        foreach (var gesture in loadedGestures)
                        {
                            _gestureMaps.Add(gesture);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定の読み込みに失敗しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveGestures()
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<GestureMap>));
                using (FileStream fs = new FileStream(SETTINGS_FILE, FileMode.Create))
                {
                    serializer.Serialize(fs, _gestureMaps.ToList());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定の保存に失敗しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EndEditOnMouseDown(object sender, MouseEventArgs e)
        {
            if (dgvGestures.IsCurrentCellInEditMode)
            {
                dgvGestures.EndEdit();
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
            _btnAddGesture = new Button();
            _btnAddGesture.Text = "追加";
            _btnAddGesture.Size = new Size(100, 35);
            _btnAddGesture.Location = new Point(dgvGestures.Right - 100, dgvGestures.Bottom + 10); 
            
            _btnAddGesture.FlatStyle = FlatStyle.Flat;
            _btnAddGesture.FlatAppearance.BorderSize = 0;
            _btnAddGesture.BackColor = Color.FromArgb(0, 122, 204);
            _btnAddGesture.ForeColor = Color.White;
            _btnAddGesture.Font = new Font("Meiryo UI", 10, FontStyle.Bold); // Use Japanese font
            _btnAddGesture.Cursor = Cursors.Hand;
            _btnAddGesture.Click += BtnAddGesture_Click;
            
            this.Controls.Add(_btnAddGesture);
            _btnAddGesture.BringToFront();
        }

        private void BtnAddGesture_Click(object sender, EventArgs e)
        {
            _gestureMaps.Add(new GestureMap());
            
            if (dgvGestures.Rows.Count > 0)
            {
                int lastRowIndex = dgvGestures.Rows.Count - 1;
                dgvGestures.FirstDisplayedScrollingRowIndex = lastRowIndex;
            }
        }

        private void ApplyModernTheme()
        {
            // Form
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Meiryo UI", 10); // Japanese Font
            this.Text = "マウスジェスチャーアプリ";

            // DataGridView
            dgvGestures.BackgroundColor = Color.FromArgb(30, 30, 30);
            dgvGestures.BorderStyle = BorderStyle.FixedSingle; // Frame the table
            dgvGestures.GridColor = Color.FromArgb(50, 50, 50);
            dgvGestures.EnableHeadersVisualStyles = false;
            dgvGestures.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgvGestures.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 122, 204);
            dgvGestures.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvGestures.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 122, 204);
            dgvGestures.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
            dgvGestures.DefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
            dgvGestures.DefaultCellStyle.ForeColor = Color.White;
            dgvGestures.DefaultCellStyle.SelectionBackColor = Color.FromArgb(62, 62, 66);
            dgvGestures.DefaultCellStyle.SelectionForeColor = Color.White;

            // Label
            lblGesture.ForeColor = Color.White;
            
            if (_btnAddGesture != null)
            {
                 _btnAddGesture.Location = new Point(dgvGestures.Right - _btnAddGesture.Width, dgvGestures.Bottom + 10);
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
                var col = dgvGestures.Columns[e.ColumnIndex];
                if (col.Name == "Name" || col.HeaderText == "名前")
                {
                    dgvGestures.BeginEdit(true);
                }
            }
        }

        private void DgvGestures_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            _editingRowIndex = e.RowIndex;

            if (_editingRowIndex >= _gestureMaps.Count)
            {
                _gestureMaps.Add(new GestureMap());
            }

            string headerText = dgvGestures.Columns[e.ColumnIndex].HeaderText;

            if (headerText == "ジェスチャー") // Gesture
            {
                _isRecordingGesture = true;
                UpdateGestureLabel($"行 {e.RowIndex + 1} のジェスチャーを記録中...");
                dgvGestures.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.DarkRed;
            }
            else if (headerText == "キー割り当て") // KeyBinding
            {
                _isListeningForKey = true;
                UpdateGestureLabel($"行 {e.RowIndex + 1} のキーを押してください...");
                dgvGestures.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.DarkBlue;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_isListeningForKey && _editingRowIndex >= 0 && _editingRowIndex < _gestureMaps.Count)
            {
                Keys keyCode = keyData & Keys.KeyCode;
                bool isModifier = keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey || keyCode == Keys.Menu;

                if (!isModifier)
                {
                    string sendKeysFormat = ConvertToSendKeysFormat(keyData);
                    _gestureMaps[_editingRowIndex].KeyBinding = sendKeysFormat;
                    
                    foreach (DataGridViewColumn col in dgvGestures.Columns)
                    {
                        if (col.HeaderText == "キー割り当て")
                        {
                            dgvGestures.Rows[_editingRowIndex].Cells[col.Index].Style.BackColor = dgvGestures.DefaultCellStyle.BackColor;
                            break;
                        }
                    }
                    
                    _isListeningForKey = false;
                    _editingRowIndex = -1;
                    UpdateGestureLabel("キーを割り当てました。");
                    dgvGestures.Refresh();
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private string ConvertToSendKeysFormat(Keys keyData)
        {
            StringBuilder sb = new StringBuilder();
            if ((keyData & Keys.Control) == Keys.Control) sb.Append("^");
            if ((keyData & Keys.Alt) == Keys.Alt) sb.Append("%");
            if ((keyData & Keys.Shift) == Keys.Shift) sb.Append("+");

            Keys keyCode = keyData & Keys.KeyCode;

            switch (keyCode)
            {
                case Keys.Enter: return sb.Append("{ENTER}").ToString();
                case Keys.Tab: return sb.Append("{TAB}").ToString();
                case Keys.Back: return sb.Append("{BACKSPACE}").ToString();
                case Keys.Delete: return sb.Append("{DELETE}").ToString();
                case Keys.Escape: return sb.Append("{ESC}").ToString();
                default:
                    if (keyCode >= Keys.A && keyCode <= Keys.Z)
                        return sb.Append(keyCode.ToString().ToLower()).ToString();
                    else if (keyCode >= Keys.D0 && keyCode <= Keys.D9)
                        return sb.Append(keyCode.ToString().Replace("D", "")).ToString();
                    else
                        return sb.Append("{" + keyCode.ToString().ToUpper() + "}").ToString();
            }
        }

        private void OnRightButtonDown(object sender, MouseHookEventArgs e)
        {
            _isRightButtonDown = true;
            _startPoint = e.Point;
            _gestureDirections.Clear();
            
            UpdateRealTimeFeedback("", new Point(e.Point.X, e.Point.Y));
            
            e.Handled = true;
        }

        private void OnRightButtonUp(object sender, MouseHookEventArgs e)
        {
            _isRightButtonDown = false;
            HideRealTimeFeedback();
            
            string finalGesture = string.Join("", _gestureDirections);
            e.Handled = true;

            if (_isRecordingGesture && _editingRowIndex >= 0 && _editingRowIndex < _gestureMaps.Count)
            {
                if (!string.IsNullOrEmpty(finalGesture))
                {
                    _gestureMaps[_editingRowIndex].Gesture = finalGesture;
                    UpdateGestureLabel($"記録完了: {finalGesture}");
                }
                else
                {
                    UpdateGestureLabel("記録キャンセル");
                }
                
                foreach (DataGridViewColumn col in dgvGestures.Columns)
                {
                    if (col.HeaderText == "ジェスチャー")
                    {
                        dgvGestures.Rows[_editingRowIndex].Cells[col.Index].Style.BackColor = dgvGestures.DefaultCellStyle.BackColor;
                        break;
                    }
                }

                _isRecordingGesture = false;
                _editingRowIndex = -1;
                dgvGestures.Refresh();
            }
            else
            {
                if (!string.IsNullOrEmpty(finalGesture))
                {
                    UpdateGestureLabel($"ジェスチャー: {finalGesture}");
                    ExecuteGesture(finalGesture);
                }
                else
                {
                    UpdateGestureLabel("右クリック");
                    SendRightClick(e.Point);
                }
            }
        }

        private void SendRightClick(MousePoint p)
        {
            _mouseHook.Paused = true;
            MouseHook.mouse_event(MouseHook.MOUSEEVENTF_RIGHTDOWN, p.X, p.Y, 0, 0);
            MouseHook.mouse_event(MouseHook.MOUSEEVENTF_RIGHTUP, p.X, p.Y, 0, 0);
            _mouseHook.Paused = false;
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
                    UpdateGestureLabel(string.Join(" -> ", _gestureDirections));
                    _startPoint = e.Point;
                }
            }
            
            string currentGesture = string.Join("", _gestureDirections);
            UpdateRealTimeFeedback(currentGesture, new Point(e.Point.X, e.Point.Y));
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
                UpdateGestureLabel(string.Join(" -> ", _gestureDirections));
                UpdateRealTimeFeedback(currentGesture, new Point(e.Point.X, e.Point.Y));
            }
            
            e.Handled = true;
        }

        private void OnLeftButtonDown(object sender, MouseHookEventArgs e)
        {
            if (!_isRightButtonDown) return;
            AddClickGesture("Left", e);
        }

        private void OnMiddleButtonDown(object sender, MouseHookEventArgs e)
        {
            if (!_isRightButtonDown) return;
            AddClickGesture("Mid", e);
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
            UpdateGestureLabel(string.Join(" -> ", _gestureDirections));
            UpdateRealTimeFeedback(currentGesture, new Point(e.Point.X, e.Point.Y));
            
            e.Handled = true;
        }

        private void UpdateRealTimeFeedback(string gesture, Point location)
        {
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
                _feedbackLabel.Font = new Font("Meiryo UI", 12, FontStyle.Bold);
                _feedbackLabel.AutoSize = true;
                _feedbackLabel.Location = new Point(5, 5);
                
                _feedbackForm.Controls.Add(_feedbackLabel);
            }

            string displayText = gesture;
            var match = _gestureMaps.FirstOrDefault(m => m.Gesture == gesture);
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
            if (lblGesture.InvokeRequired)
                lblGesture.Invoke(new Action(() => lblGesture.Text = text));
            else
                lblGesture.Text = text;
        }

        private void ExecuteGesture(string gesture)
        {
            var map = _gestureMaps.FirstOrDefault(m => m.Gesture == gesture);
            if (map != null)
            {
                if (!string.IsNullOrEmpty(map.KeyBinding))
                {
                    try
                    {
                        SendKeys.Send(map.KeyBinding);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error sending keys: {ex.Message}");
                    }
                }
            }
        }
    }

    public class GestureMap
    {
        [DisplayName("名前")]
        public string Name { get; set; }
        
        [DisplayName("ジェスチャー")]
        public string Gesture { get; set; }
        
        [DisplayName("キー割り当て")]
        public string KeyBinding { get; set; }
    }
}
