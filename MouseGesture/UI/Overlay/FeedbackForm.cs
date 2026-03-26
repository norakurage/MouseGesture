using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    /// <summary>
    /// フォーカスを奪わないオーバーレイフォーム。
    /// WS_EX_NOACTIVATE スタイルにより ShowWithoutActivation が実現される。
    /// </summary>
    internal class FeedbackForm : Form
    {
        // CreateParams でアクティベーション不要フラグを設定
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // WS_EX_NOACTIVATE: フォームをクリックしてもアクティブ化しない
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                // WS_EX_TOOLWINDOW: Alt+Tab 一覧に表示しない
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        // ShowWithoutActivation = true でウィンドウを表示してもフォーカスを奪わない
        protected override bool ShowWithoutActivation => true;
    }
}
