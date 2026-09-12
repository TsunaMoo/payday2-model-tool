namespace PD2ModelParser.UI
{
    partial class HelpPanel
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            creditLabel = new System.Windows.Forms.Label();
            SuspendLayout();
            // 
            // creditLabel
            // 
            creditLabel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            creditLabel.AutoSize = true;
            creditLabel.ForeColor = System.Drawing.SystemColors.ControlText;
            creditLabel.Location = new System.Drawing.Point(386, 365);
            creditLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            creditLabel.Name = "creditLabel";
            creditLabel.Size = new System.Drawing.Size(245, 15);
            creditLabel.TabIndex = 7;
            creditLabel.Text = "Credit to ZNixian, PoueT and I am not a spy...";
            // 
            // HelpPanel
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(creditLabel);
            ForeColor = System.Drawing.SystemColors.Control;
            Name = "HelpPanel";
            Size = new System.Drawing.Size(645, 395);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label creditLabel;
    }
}
