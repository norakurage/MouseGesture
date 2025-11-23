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
            this.btnRecord = new System.Windows.Forms.Button();
            this.txtRecordedGesture = new System.Windows.Forms.TextBox();
            this.btnKeyBinding = new System.Windows.Forms.Button();
            this.btnAdd = new System.Windows.Forms.Button();
            this.dgvGestures = new System.Windows.Forms.DataGridView();
            this.lblGesture = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dgvGestures)).BeginInit();
            this.SuspendLayout();
            // 
            // btnRecord
            // 
            this.btnRecord.Location = new System.Drawing.Point(12, 50);
            this.btnRecord.Name = "btnRecord";
            this.btnRecord.Size = new System.Drawing.Size(75, 23);
            this.btnRecord.TabIndex = 1;
            this.btnRecord.Text = "Record";
            this.btnRecord.UseVisualStyleBackColor = true;
            this.btnRecord.Click += new System.EventHandler(this.btnRecord_Click);
            // 
            // txtRecordedGesture
            // 
            this.txtRecordedGesture.Location = new System.Drawing.Point(93, 52);
            this.txtRecordedGesture.Name = "txtRecordedGesture";
            this.txtRecordedGesture.ReadOnly = true;
            this.txtRecordedGesture.Size = new System.Drawing.Size(100, 19);
            this.txtRecordedGesture.TabIndex = 2;
            // 
            // btnKeyBinding
            // 
            this.btnKeyBinding.Location = new System.Drawing.Point(199, 50);
            this.btnKeyBinding.Name = "btnKeyBinding";
            this.btnKeyBinding.Size = new System.Drawing.Size(100, 23);
            this.btnKeyBinding.TabIndex = 3;
            this.btnKeyBinding.Text = "None";
            this.btnKeyBinding.UseVisualStyleBackColor = true;
            this.btnKeyBinding.Click += new System.EventHandler(this.btnKeyBinding_Click);
            // 
            // btnAdd
            // 
            this.btnAdd.Location = new System.Drawing.Point(305, 50);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(75, 23);
            this.btnAdd.TabIndex = 4;
            this.btnAdd.Text = "Add";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // dgvGestures
            // 
            this.dgvGestures.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvGestures.Location = new System.Drawing.Point(12, 79);
            this.dgvGestures.Name = "dgvGestures";
            this.dgvGestures.RowTemplate.Height = 21;
            this.dgvGestures.Size = new System.Drawing.Size(368, 150);
            this.dgvGestures.TabIndex = 5;
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
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.dgvGestures);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.btnKeyBinding);
            this.Controls.Add(this.txtRecordedGesture);
            this.Controls.Add(this.btnRecord);
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
        private System.Windows.Forms.Button btnRecord;
        private System.Windows.Forms.TextBox txtRecordedGesture;
        private System.Windows.Forms.Button btnKeyBinding;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.DataGridView dgvGestures;
    }
}

