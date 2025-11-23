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
        private const int MOVEMENT_THRESHOLD = 20; // Minimum pixels to register a movement

        private bool _isRecording = false;
        private Dictionary<string, string> _gestureMappings = new Dictionary<string, string>();
        private BindingSource _bindingSource = new BindingSource();

        private bool _isListeningForKey = false;
        private string _currentKeyBinding = "";

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (IsDesignMode()) return;

            _mouseHook = new MouseHook();
            _mouseHook.RightButtonDown += OnRightButtonDown;
            _mouseHook.RightButtonUp += OnRightButtonUp;
            _mouseHook.MouseMove += OnMouseMove;
            _mouseHook.Start();

            _bindingSource.DataSource = _gestureMappings.Select(kv => new { Gesture = kv.Key, Key = kv.Value }).ToList();
            dgvGestures.DataSource = _bindingSource;
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

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_isListeningForKey)
            {
                // Check if the pressed key is just a modifier
                Keys keyCode = keyData & Keys.KeyCode;
                bool isModifier = keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey || keyCode == Keys.Menu;

                // Update display
                string keyDisplay = new KeysConverter().ConvertToString(keyData);
                btnKeyBinding.Text = keyDisplay;

                // Only finish if it's NOT just a modifier key
                if (!isModifier)
                {
                    // Convert to SendKeys format
                    _currentKeyBinding = ConvertToSendKeysFormat(keyData);
                    _isListeningForKey = false;
                }

                return true; // Consume the key event
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

            // Handle special keys
            switch (keyCode)
            {
                case Keys.Enter: return sb.Append("{ENTER}").ToString();
                case Keys.Tab: return sb.Append("{TAB}").ToString();
                case Keys.Back: return sb.Append("{BACKSPACE}").ToString();
                case Keys.Delete: return sb.Append("{DELETE}").ToString();
                case Keys.Escape: return sb.Append("{ESC}").ToString();
                // Add more special keys as needed
                default:
                    if (keyCode >= Keys.A && keyCode <= Keys.Z)
                    {
                        return sb.Append(keyCode.ToString().ToLower()).ToString();
                    }
                    else if (keyCode >= Keys.D0 && keyCode <= Keys.D9)
                    {
                        return sb.Append(keyCode.ToString().Replace("D", "")).ToString();
                    }
                    else
                    {
                        // Fallback for other keys, might need more robust handling
                        return sb.Append("{" + keyCode.ToString().ToUpper() + "}").ToString();
                    }
            }
        }

        private void OnRightButtonDown(object sender, MouseHookEventArgs e)
        {
            _isRightButtonDown = true;
            _startPoint = e.Point;
            _gestureDirections.Clear();
            UpdateGestureLabel("Listening...");

            // Suppress the default right-click menu
            e.Handled = true;
        }

        private void OnRightButtonUp(object sender, MouseHookEventArgs e)
        {
            _isRightButtonDown = false;
            string finalGesture = string.Join("", _gestureDirections);

            // Suppress the default right-click menu
            e.Handled = true;

            if (_isRecording)
            {
                if (!string.IsNullOrEmpty(finalGesture))
                {
                    txtRecordedGesture.Invoke(new Action(() => txtRecordedGesture.Text = finalGesture));
                    _isRecording = false;
                    btnRecord.Invoke(new Action(() => btnRecord.Text = "Record"));
                    UpdateGestureLabel($"Recorded: {finalGesture}");
                }
                else
                {
                    UpdateGestureLabel("Recording Cancelled (No Gesture)");
                    _isRecording = false;
                    btnRecord.Invoke(new Action(() => btnRecord.Text = "Record"));
                    
                    // If no gesture during recording, maybe we should let the click through?
                    // For now, let's just cancel recording.
                }
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
                    // No gesture detected, so it was a normal right click.
                    // We suppressed it, so now we must manually send it.
                    SendRightClick(e.Point);
                }
            }
        }

        private void SendRightClick(MousePoint p)
        {
            // Temporarily stop the hook to avoid infinite loop
            _mouseHook.Stop();

            // Send Right Down and Right Up
            MouseHook.mouse_event(MouseHook.MOUSEEVENTF_RIGHTDOWN, p.X, p.Y, 0, 0);
            MouseHook.mouse_event(MouseHook.MOUSEEVENTF_RIGHTUP, p.X, p.Y, 0, 0);

            // Restart the hook
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
                {
                    direction = dx > 0 ? "R" : "L";
                }
                else
                {
                    direction = dy > 0 ? "D" : "U";
                }

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
            {
                lblGesture.Invoke(new Action(() => lblGesture.Text = text));
            }
            else
            {
                lblGesture.Text = text;
            }
        }

        private void ExecuteGesture(string gesture)
        {
            if (_gestureMappings.ContainsKey(gesture))
            {
                string key = _gestureMappings[gesture];
                try
                {
                    SendKeys.Send(key);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error sending keys: {ex.Message}");
                }
            }
        }

        private void btnRecord_Click(object sender, EventArgs e)
        {
            _isRecording = true;
            btnRecord.Text = "Recording...";
            txtRecordedGesture.Text = "";
            UpdateGestureLabel("Perform a gesture to record...");
        }

        private void btnKeyBinding_Click(object sender, EventArgs e)
        {
            _isListeningForKey = true;
            btnKeyBinding.Text = "Press Key...";
            _currentKeyBinding = "";
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            string gesture = txtRecordedGesture.Text;
            string key = _currentKeyBinding;

            if (string.IsNullOrEmpty(gesture) || string.IsNullOrEmpty(key))
            {
                MessageBox.Show("Please record a gesture and assign a key binding.");
                return;
            }

            if (_gestureMappings.ContainsKey(gesture))
            {
                _gestureMappings[gesture] = key;
            }
            else
            {
                _gestureMappings.Add(gesture, key);
            }

            RefreshGrid();
            txtRecordedGesture.Text = "";
            btnKeyBinding.Text = "None";
            _currentKeyBinding = "";
            MessageBox.Show("Mapping added!");
        }

        private void RefreshGrid()
        {
            _bindingSource.DataSource = _gestureMappings.Select(kv => new { Gesture = kv.Key, Key = kv.Value }).ToList();
            dgvGestures.DataSource = _bindingSource;
        }
    }
}
