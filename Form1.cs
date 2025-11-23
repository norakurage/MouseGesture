using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private MouseHook _mouseHook;
        private bool _isRightButtonDown = false;
        private MousePoint _startPoint;
        private List<string> _gestureDirections = new List<string>();
        private const int MOVEMENT_THRESHOLD = 20;

        // Data binding
        private BindingList<GestureMap> _gestureMaps = new BindingList<GestureMap>();

        // Editing state
        private int _editingRowIndex = -1;
        private bool _isRecordingGesture = false;
        private bool _isListeningForKey = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (IsDesignMode()) return;

            ApplyModernTheme();

            _mouseHook = new MouseHook();
            _mouseHook.RightButtonDown += OnRightButtonDown;
            _mouseHook.RightButtonUp += OnRightButtonUp;
            _mouseHook.MouseMove += OnMouseMove;
            _mouseHook.Start();

            // Setup Grid
            dgvGestures.DataSource = _gestureMaps;
            dgvGestures.CellDoubleClick += DgvGestures_CellDoubleClick;
        }

        private void ApplyModernTheme()
        {
            // Form
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);

            // DataGridView
            dgvGestures.BackgroundColor = Color.FromArgb(30, 30, 30);
            dgvGestures.BorderStyle = BorderStyle.None;
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
        }

        private void StyleTextBox(TextBox txt)
        {
            txt.BackColor = Color.FromArgb(45, 45, 48);
            txt.ForeColor = Color.White;
            txt.BorderStyle = BorderStyle.FixedSingle;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _mouseHook?.Stop();
            base.OnFormClosing(e);
        }

        private bool IsDesignMode()
        {
            return LicenseManager.UsageMode == LicenseUsageMode.Designtime;
        }

        private void DgvGestures_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            _editingRowIndex = e.RowIndex;

            // Ensure row exists in the underlying list (handle "new row" placeholder)
            if (_editingRowIndex >= _gestureMaps.Count)
            {
                _gestureMaps.Add(new GestureMap());
            }

            // Column 0: Gesture, Column 1: KeyBinding (Assuming auto-generation order, but better to check names if possible. 
            // For simplicity with auto-gen, we assume properties order: Gesture, KeyBinding)
            
            if (e.ColumnIndex == 0) // Gesture
            {
                _isRecordingGesture = true;
                UpdateGestureLabel($"Recording Gesture for Row {e.RowIndex + 1}...");
                dgvGestures.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.DarkRed;
            }
            else if (e.ColumnIndex == 1) // KeyBinding
            {
                _isListeningForKey = true;
                UpdateGestureLabel($"Press Key for Row {e.RowIndex + 1}...");
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
                    
                    // Reset style
                    dgvGestures.Rows[_editingRowIndex].Cells[1].Style.BackColor = dgvGestures.DefaultCellStyle.BackColor;
                    
                    _isListeningForKey = false;
                    _editingRowIndex = -1;
                    UpdateGestureLabel("Key Assigned.");
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
            UpdateGestureLabel("Listening...");
            e.Handled = true;
        }

        private void OnRightButtonUp(object sender, MouseHookEventArgs e)
        {
            _isRightButtonDown = false;
            string finalGesture = string.Join("", _gestureDirections);
            e.Handled = true;

            if (_isRecordingGesture && _editingRowIndex >= 0 && _editingRowIndex < _gestureMaps.Count)
            {
                if (!string.IsNullOrEmpty(finalGesture))
                {
                    _gestureMaps[_editingRowIndex].Gesture = finalGesture;
                    UpdateGestureLabel($"Recorded: {finalGesture}");
                }
                else
                {
                    UpdateGestureLabel("Recording Cancelled");
                }
                
                // Reset style
                dgvGestures.Rows[_editingRowIndex].Cells[0].Style.BackColor = dgvGestures.DefaultCellStyle.BackColor;

                _isRecordingGesture = false;
                _editingRowIndex = -1;
                dgvGestures.Refresh();
            }
            else
            {
                if (!string.IsNullOrEmpty(finalGesture))
                {
                    UpdateGestureLabel($"Gesture: {finalGesture}");
                    ExecuteGesture(finalGesture);
                }
                else
                {
                    UpdateGestureLabel("Right Click");
                    SendRightClick(e.Point);
                }
            }
        }

        private void SendRightClick(MousePoint p)
        {
            _mouseHook.Stop();
            MouseHook.mouse_event(MouseHook.MOUSEEVENTF_RIGHTDOWN, p.X, p.Y, 0, 0);
            MouseHook.mouse_event(MouseHook.MOUSEEVENTF_RIGHTUP, p.X, p.Y, 0, 0);
            _mouseHook.Start();
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
                    direction = dx > 0 ? "R" : "L";
                else
                    direction = dy > 0 ? "D" : "U";

                if (_gestureDirections.Count == 0 || _gestureDirections.Last() != direction)
                {
                    _gestureDirections.Add(direction);
                    UpdateGestureLabel(string.Join(" -> ", _gestureDirections));
                    _startPoint = e.Point;
                }
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
            if (map != null && !string.IsNullOrEmpty(map.KeyBinding))
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

    public class GestureMap
    {
        public string Gesture { get; set; }
        public string KeyBinding { get; set; }
    }
}
