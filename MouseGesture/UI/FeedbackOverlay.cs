using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp1.UI
{
    public class FeedbackOverlay : IDisposable
    {
        private Form _feedbackForm;
        private Label _feedbackLabel;
        private Timer _feedbackFadeTimer;
        private bool _isDisposed = false;

        public FeedbackOverlay()
        {
            _feedbackFadeTimer = new Timer();
            _feedbackFadeTimer.Interval = 16; // ~60fps fade
            _feedbackFadeTimer.Tick += OnFadeTick;
        }

        private void OnFadeTick(object sender, EventArgs args)
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
        }

        public void ShowGesture(string displayText, Point location)
        {
            if (_isDisposed) return;

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
                         var oldFont = _feedbackLabel.Font;
                         _feedbackLabel.Font = new Font("Meiryo UI", targetPixelSize, FontStyle.Bold, GraphicsUnit.Pixel);
                         oldFont.Dispose();
                     }
                }
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
                _feedbackFadeTimer?.Stop();
                _feedbackForm.Opacity = 0.8;
            }
        }

        public void Hide()
        {
            if (_isDisposed) return;

            if (_feedbackForm != null && !_feedbackForm.IsDisposed && _feedbackForm.Visible)
            {
                if (_feedbackFadeTimer != null && !_feedbackFadeTimer.Enabled)
                {
                    _feedbackFadeTimer.Start();
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _feedbackFadeTimer?.Stop();
            _feedbackFadeTimer?.Dispose();

            if (_feedbackForm != null)
            {
                if (!_feedbackForm.IsDisposed)
                {
                    _feedbackForm.Dispose();
                }
                _feedbackForm = null;
            }
        }
    }
}
