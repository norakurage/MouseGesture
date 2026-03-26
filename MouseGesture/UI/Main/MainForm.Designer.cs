namespace WindowsFormsApp1
{
    partial class Form1
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージド リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナーで生成されたコード

        /// <summary>
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            this.dgvGestures = new System.Windows.Forms.DataGridView();
            this.lblGesture = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dgvGestures)).BeginInit();
            this.SuspendLayout();
            // 
            // dgvGestures
            // 
            this.dgvGestures.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvGestures.Location = new System.Drawing.Point(12, 50);
            this.dgvGestures.Name = "dgvGestures";
            this.dgvGestures.RowTemplate.Height = 21;
            this.dgvGestures.Size = new System.Drawing.Size(460, 300);
            this.dgvGestures.TabIndex = 5;
            this.dgvGestures.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            // 
            // lblGesture
            // 
            this.lblGesture.AutoSize = true;
            this.lblGesture.Font = new System.Drawing.Font("MS UI Gothic", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.lblGesture.Location = new System.Drawing.Point(12, 9);
            this.lblGesture.Name = "lblGesture";
            this.lblGesture.Size = new System.Drawing.Size(0, 33);
            this.lblGesture.TabIndex = 0;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 411);
            this.Controls.Add(this.dgvGestures);
            this.Controls.Add(this.lblGesture);
            this.Name = "Form1";
            this.Text = "Mouse Gesture App";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvGestures)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label lblGesture;
        private System.Windows.Forms.DataGridView dgvGestures;
    }
}

