using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public class GestureEngine : IDisposable
    {
        private MouseHook _mouseHook;
        private ProfileManager _profileManager;

        private bool _isRightButtonDown = false;
        private MousePoint _startPoint;
        private List<string> _gestureDirections = new List<string>();
        private const int MOVEMENT_THRESHOLD = 20;
        private const int MAX_GESTURE_STEPS = 8;

        private bool _gestureExecuted = false;
        private int _lastFeedbackUpdateTick = 0;
        private const int FEEDBACK_UPDATE_INTERVAL_MS = 7; // 7ms = 144fps

        private IntPtr _targetWindowHandle = IntPtr.Zero;

        // MainFormとの連携用コールバック（フィードバックUIなどの更新）
        public Action<string, Point> OnUpdateFeedback { get; set; }
        public Action OnHideFeedback { get; set; }
        public Action<int> OnHideFeedbackDelayed { get; set; }
        public Func<bool> IsRecordingGesture { get; set; }
        public Func<bool> IsListeningForKey { get; set; }
        public Action<string> OnGestureRecorded { get; set; }
        public Action OnCancelEditingMode { get; set; }

        public GestureEngine(ProfileManager profileManager)
        {
            _profileManager = profileManager;

            _mouseHook = new MouseHook();
            _mouseHook.RightButtonDown += OnRightButtonDown;
            _mouseHook.RightButtonUp += OnRightButtonUp;
            _mouseHook.LeftButtonDown += OnLeftButtonDown;
            _mouseHook.MiddleButtonDown += OnMiddleButtonDown;
            _mouseHook.XButtonDown += OnXButtonDown;
            _mouseHook.MouseMove += OnMouseMove;
            _mouseHook.MouseWheel += OnMouseWheel;
        }

        public void Start()
        {
            _mouseHook.Start();
        }

        public void Stop()
        {
            _mouseHook.Stop();
        }

        private void OnRightButtonDown(object sender, MouseHookEventArgs e)
        {
            string currentAppPath = ProcessUtils.GetActiveProcessPath();
            if (_profileManager.IsAppDisabled(currentAppPath))
            {
                return;
            }

            _targetWindowHandle = NativeMethods.GetForegroundWindow();

            _isRightButtonDown = true;
            _gestureExecuted = false;
            _startPoint = e.Point;
            _gestureDirections.Clear();

            // 即時消去
            OnHideFeedback?.Invoke();
            
            OnUpdateFeedback?.Invoke("", new Point(e.Point.X, e.Point.Y));
            
            e.Handled = true;
        }

        private void OnRightButtonUp(object sender, MouseHookEventArgs e)
        {
            if (!_isRightButtonDown) return;

            _isRightButtonDown = false;
            OnHideFeedback?.Invoke();
            
            string finalGesture = string.Join("", _gestureDirections);
            e.Handled = true;

            // Gesture Recording (UI state integration)
            if (IsRecordingGesture != null && IsRecordingGesture())
            {
                if (!string.IsNullOrEmpty(finalGesture))
                {
                    OnGestureRecorded?.Invoke(finalGesture);
                }
                else
                {
                    OnCancelEditingMode?.Invoke();
                }
                return;
            }

            if (IsListeningForKey != null && IsListeningForKey())
            {
                OnCancelEditingMode?.Invoke();
                return;
            }

            if (_gestureExecuted)
            {
                // Do nothing
            }
            else if (!string.IsNullOrEmpty(finalGesture))
            {
                ExecuteGesture(finalGesture, currentAppPath: ProcessUtils.GetActiveProcessPath());
            }
            else
            {
                SendRightClick(e.Point);
            }
        }

        private void SendRightClick(MousePoint p)
        {
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
            
            int now = Environment.TickCount;
            if (unchecked(now - _lastFeedbackUpdateTick) >= FEEDBACK_UPDATE_INTERVAL_MS)
            {
                _lastFeedbackUpdateTick = now;
                OnUpdateFeedback?.Invoke(currentGesture, new Point(e.Point.X, e.Point.Y));
            }
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
                OnUpdateFeedback?.Invoke(currentGesture, new Point(e.Point.X, e.Point.Y));

                if (IsRecordingGesture?.Invoke() == true || IsListeningForKey?.Invoke() == true) return;

                var map = _profileManager.FindGestureMap(currentGesture, ProcessUtils.GetActiveProcessPath());
                if (map != null)
                {
                    ExecuteGesture(currentGesture, ProcessUtils.GetActiveProcessPath());
                    _gestureExecuted = true;
                    _gestureDirections.Clear();
                    
                    OnHideFeedbackDelayed?.Invoke(750);
                }
            }
            
            e.Handled = true;
        }

        private void OnLeftButtonDown(object sender, MouseHookEventArgs e)
        {
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
            OnUpdateFeedback?.Invoke(currentGesture, new Point(e.Point.X, e.Point.Y));
            
            e.Handled = true;

            if (IsRecordingGesture?.Invoke() == true || IsListeningForKey?.Invoke() == true) return;

            GestureMap map = _profileManager.FindGestureMap(currentGesture, ProcessUtils.GetActiveProcessPath());

            if (map != null)
            {
                ExecuteGesture(currentGesture, ProcessUtils.GetActiveProcessPath());
                _gestureExecuted = true;
                _gestureDirections.Clear();
                _startPoint = e.Point;

                OnHideFeedbackDelayed?.Invoke(750);
            }
        }

        private void ExecuteGesture(string gesture, string currentAppPath)
        {
            GestureMap map = _profileManager.FindGestureMap(gesture, currentAppPath);

            if (map != null && !string.IsNullOrEmpty(map.KeyBinding))
            {
                try
                {
                    string sendKeys = KeyUtils.ConvertReadableToSendKeys(map.KeyBinding);
                    
                    // WPF等の特定アプリケーションではSendKeysがフォーカスを失うと動作しないため、
                    // UIスレッドで実行させるようMainForm側でラップしてもよいが、今回はシンプルに実行する。
                    // sendKeysの処理はUIスレッドで回した方が良いのでInvokeなどが必要な場合は外部からActionで渡すか、
                    // ここでSendKeys.SendWaitを行ってもよい。元のコードにならってここではSendKeys.Send(SendWait推奨？)を使う
                    SendKeys.SendWait(sendKeys);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending keys: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
