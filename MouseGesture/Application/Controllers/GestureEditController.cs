using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public class GestureEditController : IDisposable
    {
        private ProfileManager _profileManager;
        private Action<string> _updateLabelAction;
        private GestureEngine _gestureEngine;

        // 状態管理：インデックスではなくオブジェクト参照で保持（レビュー指摘①）
        private GestureMap _editingItem;
        
        public bool IsRecordingGesture { get; private set; } = false;
        public bool IsListeningForKey { get; private set; } = false;
        
        private DataGridView _editingGrid;

        public GestureEditController(ProfileManager profileManager, Action<string> updateLabelAction)
        {
            _profileManager = profileManager;
            _updateLabelAction = updateLabelAction;
        }

        public void InitializeEngine(
            Action<string, Point> onUpdateFeedback,
            Action onHideFeedback,
            Action<int> onHideFeedbackDelayed,
            Action<Action> invokeAction)
        {
            _gestureEngine = new GestureEngine(_profileManager)
            {
                OnUpdateFeedback = onUpdateFeedback,
                OnHideFeedback = onHideFeedback,
                OnHideFeedbackDelayed = onHideFeedbackDelayed,
                IsRecordingGesture = () => this.IsRecordingGesture,
                IsListeningForKey = () => this.IsListeningForKey,
                OnCancelEditingMode = () => invokeAction(this.CancelEditingMode),
                OnGestureRecorded = (gesture) => invokeAction(() => this.OnGestureRecorded(gesture))
            };
        }

        public void StartEngine() => _gestureEngine?.Start();
        public void StopEngine() => _gestureEngine?.Stop();
        public void Dispose() => _gestureEngine?.Dispose();

        public void Attach(DataGridView dgv)
        {
            dgv.CellDoubleClick += Dgv_CellDoubleClick;
            dgv.CellClick += Dgv_CellClick;
            dgv.CellContentClick += Dgv_CellContentClick;
            dgv.EditingControlShowing += Dgv_EditingControlShowing;
            dgv.MouseDown += EndEditOnMouseDown;
        }

        public void HandleMouseDownOnOtherControls(object sender, MouseEventArgs e)
        {
            if (_editingGrid != null && _editingGrid.IsCurrentCellInEditMode)
            {
                _editingGrid.EndEdit();
            }
        }

        private void EndEditOnMouseDown(object sender, MouseEventArgs e)
        {
            if (sender is DataGridView dgv && dgv.IsCurrentCellInEditMode)
            {
                dgv.EndEdit();
            }
        }

        private void Dgv_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is TextBox tb)
            {
                tb.BackColor = Color.FromArgb(45, 45, 48);
                tb.ForeColor = Color.White;
                tb.BorderStyle = BorderStyle.FixedSingle;
                
                // イベントの多重登録を防ぐための予防措置（レビュー指摘③）
                // tb.KeyDown -= CustomKeyDownHandler;
                // tb.KeyDown += CustomKeyDownHandler;
            }
        }

        private void Dgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var dgv = sender as DataGridView;
                var col = dgv.Columns[e.ColumnIndex];
                
                // HeaderTextではなくNameに依存させる（レビュー指摘②）
                if (col.Name == "Name")
                {
                    dgv.BeginEdit(true);
                }
            }
        }

        private void Dgv_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var dgv = sender as DataGridView;
                var list = dgv.DataSource as BindingList<GestureMap>;

                if (dgv.Columns[e.ColumnIndex].Name == "Delete")
                {
                    if (e.RowIndex < list.Count)
                    {
                        var itemToDelete = list[e.RowIndex];
                        
                        // インデックスではなくオブジェクト参照で比較
                        if (_editingItem == itemToDelete && _editingGrid == dgv)
                        {
                            CancelEditingMode();
                        }
                        
                        list.Remove(itemToDelete);
                        _profileManager.SaveGestures();
                    }
                }
            }
        }

        private void Dgv_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var dgv = sender as DataGridView;
            var list = dgv.DataSource as BindingList<GestureMap>;

            if (e.RowIndex >= list.Count)
            {
                list.Add(new GestureMap());
            }

            // 編集対象のオブジェクトを保持（レビュー指摘①）
            _editingItem = list[e.RowIndex];
            _editingGrid = dgv;

            string colName = dgv.Columns[e.ColumnIndex].Name;

            // HeaderTextへの依存を解消（レビュー指摘②）
            if (colName == "Gesture")
            {
                IsRecordingGesture = true;
                _updateLabelAction?.Invoke("ジェスチャーを記録中... (ESCまたは右クリックでキャンセル)");
                dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.DarkRed;
            }
            else if (colName == "KeyBinding")
            {
                IsListeningForKey = true;
                _updateLabelAction?.Invoke("キーを押してください... (ESCまたは右クリックでキャンセル)");
                dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.DarkBlue;
            }
        }

        public void CancelEditingMode()
        {
            if (IsRecordingGesture || IsListeningForKey)
            {
                if (_editingGrid != null && _editingItem != null)
                {
                    var list = _editingGrid.DataSource as BindingList<GestureMap>;
                    int rowIndex = list.IndexOf(_editingItem);

                    if (rowIndex >= 0 && rowIndex < _editingGrid.Rows.Count)
                    {
                        // 列インデックスの直接指定で無駄なループを排除（レビュー指摘④）
                        if (IsRecordingGesture && _editingGrid.Columns.Contains("Gesture"))
                        {
                            _editingGrid.Rows[rowIndex].Cells["Gesture"].Style.BackColor = _editingGrid.DefaultCellStyle.BackColor;
                        }
                        if (IsListeningForKey && _editingGrid.Columns.Contains("KeyBinding"))
                        {
                            _editingGrid.Rows[rowIndex].Cells["KeyBinding"].Style.BackColor = _editingGrid.DefaultCellStyle.BackColor;
                        }
                        _editingGrid.Refresh();
                    }
                }

                ResetEditingState();
                _updateLabelAction?.Invoke("キャンセルしました。");
            }
        }

        public void OnGestureRecorded(string gesture)
        {
            if (IsRecordingGesture && _editingItem != null)
            {
                _editingItem.Gesture = gesture;
                _updateLabelAction?.Invoke($"記録完了: {gesture}");
                
                if (_editingGrid != null)
                {
                    var list = _editingGrid.DataSource as BindingList<GestureMap>;
                    int rowIndex = list.IndexOf(_editingItem);

                    if (rowIndex >= 0 && _editingGrid.Columns.Contains("Gesture"))
                    {
                        _editingGrid.Rows[rowIndex].Cells["Gesture"].Style.BackColor = _editingGrid.DefaultCellStyle.BackColor;
                    }
                    _editingGrid.Refresh();
                }

                ResetEditingState();
                _profileManager.SaveGestures();
            }
        }

        public bool HandleKeyDown(Keys keyData)
        {
            // ESCキーでのキャンセル対応（レビュー指摘⑥）
            if (keyData == Keys.Escape && (IsRecordingGesture || IsListeningForKey))
            {
                CancelEditingMode();
                return true;
            }

            if (IsListeningForKey && _editingItem != null)
            {
                Keys keyCode = keyData & Keys.KeyCode;
                bool isModifier = keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey || keyCode == Keys.Menu;

                if (!isModifier)
                {
                    string readableFormat = KeyUtils.GetReadableKeyString(keyData);
                    _editingItem.KeyBinding = readableFormat;

                    if (string.IsNullOrEmpty(_editingItem.Name))
                    {
                        _editingItem.Name = readableFormat;
                    }
                    
                    if (_editingGrid != null)
                    {
                        var list = _editingGrid.DataSource as BindingList<GestureMap>;
                        int rowIndex = list.IndexOf(_editingItem);

                        if (rowIndex >= 0 && _editingGrid.Columns.Contains("KeyBinding"))
                        {
                            _editingGrid.Rows[rowIndex].Cells["KeyBinding"].Style.BackColor = _editingGrid.DefaultCellStyle.BackColor;
                        }
                        _editingGrid.Refresh();
                    }
                    
                    ResetEditingState();
                    _updateLabelAction?.Invoke("キーを割り当てました。");
                    _profileManager.SaveGestures();
                }
                return true;
            }
            return false;
        }

        // 状態リセット処理の共通化（レビュー指摘⑤）
        private void ResetEditingState()
        {
            IsRecordingGesture = false;
            IsListeningForKey = false;
            _editingItem = null;
            _editingGrid = null;
        }
    }
}