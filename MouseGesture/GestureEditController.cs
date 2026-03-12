using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public class GestureEditController
    {
        private ProfileManager _profileManager;
        private Action<string> _updateLabelAction;

        public int EditingRowIndex { get; private set; } = -1;
        public bool IsRecordingGesture { get; private set; } = false;
        public bool IsListeningForKey { get; private set; } = false;
        
        private DataGridView _editingGrid;
        private BindingList<GestureMap> _editingList;

        public GestureEditController(ProfileManager profileManager, Action<string> updateLabelAction)
        {
            _profileManager = profileManager;
            _updateLabelAction = updateLabelAction;
        }

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
            }
        }

        private void Dgv_CellClick(object sender, DataGridViewCellEventArgs e)
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
                        if (EditingRowIndex == e.RowIndex && _editingGrid == dgv)
                        {
                            CancelEditingMode();
                        }
                        
                        list.RemoveAt(e.RowIndex);
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

            EditingRowIndex = e.RowIndex;
            _editingGrid = dgv;
            _editingList = list;

            if (EditingRowIndex >= list.Count)
            {
                list.Add(new GestureMap());
            }

            string headerText = dgv.Columns[e.ColumnIndex].HeaderText;

            if (headerText == "ジェスチャー")
            {
                IsRecordingGesture = true;
                _updateLabelAction?.Invoke($"行 {e.RowIndex + 1} のジェスチャーを記録中... (右クリックでキャンセル)");
                dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.DarkRed;
            }
            else if (headerText == "キー割り当て")
            {
                IsListeningForKey = true;
                _updateLabelAction?.Invoke($"行 {e.RowIndex + 1} のキーを押してください... (右クリックでキャンセル)");
                dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.DarkBlue;
            }
        }

        public void CancelEditingMode()
        {
            if (IsRecordingGesture || IsListeningForKey)
            {
                if (_editingGrid != null && EditingRowIndex >= 0 && EditingRowIndex < _editingGrid.Rows.Count)
                {
                    if (IsRecordingGesture)
                    {
                        foreach (DataGridViewColumn col in _editingGrid.Columns)
                        {
                            if (col.HeaderText == "ジェスチャー")
                            {
                                _editingGrid.Rows[EditingRowIndex].Cells[col.Index].Style.BackColor = _editingGrid.DefaultCellStyle.BackColor;
                                break;
                            }
                        }
                    }
                    if (IsListeningForKey)
                    {
                        foreach (DataGridViewColumn col in _editingGrid.Columns)
                        {
                            if (col.HeaderText == "キー割り当て")
                            {
                                _editingGrid.Rows[EditingRowIndex].Cells[col.Index].Style.BackColor = _editingGrid.DefaultCellStyle.BackColor;
                                break;
                            }
                        }
                    }
                    _editingGrid.Refresh();
                }

                IsRecordingGesture = false;
                IsListeningForKey = false;
                EditingRowIndex = -1;
                _editingGrid = null;
                _editingList = null;
                _updateLabelAction?.Invoke("キャンセルしました。");
            }
        }

        public void OnGestureRecorded(string gesture)
        {
            if (EditingRowIndex >= 0 && _editingList != null && EditingRowIndex < _editingList.Count)
            {
                _editingList[EditingRowIndex].Gesture = gesture;
                _updateLabelAction?.Invoke($"記録完了: {gesture}");
                
                if (_editingGrid != null)
                {
                    foreach (DataGridViewColumn col in _editingGrid.Columns)
                    {
                        if (col.HeaderText == "ジェスチャー")
                        {
                            _editingGrid.Rows[EditingRowIndex].Cells[col.Index].Style.BackColor = _editingGrid.DefaultCellStyle.BackColor;
                            break;
                        }
                    }
                    _editingGrid.Refresh();
                }
                IsRecordingGesture = false;
                EditingRowIndex = -1;
                _editingGrid = null;
                _editingList = null;
                _profileManager.SaveGestures();
            }
        }

        public bool HandleKeyDown(Keys keyData)
        {
            if (IsListeningForKey && EditingRowIndex >= 0 && _editingList != null && EditingRowIndex < _editingList.Count)
            {
                Keys keyCode = keyData & Keys.KeyCode;
                bool isModifier = keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey || keyCode == Keys.Menu;

                if (!isModifier)
                {
                    string readableFormat = KeyUtils.GetReadableKeyString(keyData);
                    _editingList[EditingRowIndex].KeyBinding = readableFormat;

                    if (string.IsNullOrEmpty(_editingList[EditingRowIndex].Name))
                    {
                        _editingList[EditingRowIndex].Name = readableFormat;
                    }
                    
                    if (_editingGrid != null)
                    {
                        foreach (DataGridViewColumn col in _editingGrid.Columns)
                        {
                            if (col.HeaderText == "キー割り当て")
                            {
                                _editingGrid.Rows[EditingRowIndex].Cells[col.Index].Style.BackColor = _editingGrid.DefaultCellStyle.BackColor;
                                break;
                            }
                        }
                        _editingGrid.Refresh();
                    }
                    
                    IsListeningForKey = false;
                    EditingRowIndex = -1;
                    _editingGrid = null;
                    _editingList = null;
                    _updateLabelAction?.Invoke("キーを割り当てました。");
                    _profileManager.SaveGestures();
                }
                return true;
            }
            return false;
        }
    }
}
