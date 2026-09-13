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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HelpPanel));
            richTextBox = new System.Windows.Forms.RichTextBox();
            SuspendLayout();
            // 
            // richTextBox
            // 
            richTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            richTextBox.BackColor = System.Drawing.SystemColors.ControlLight;
            richTextBox.Location = new System.Drawing.Point(5, 5);
            richTextBox.Margin = new System.Windows.Forms.Padding(5);
            richTextBox.Name = "richTextBox";
            richTextBox.ReadOnly = true;
            richTextBox.Size = new System.Drawing.Size(698, 380);
            richTextBox.TabIndex = 9;
            richTextBox.Text = resources.GetString("richTextBox.Text");
            // 
            // HelpPanel
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(richTextBox);
            ForeColor = System.Drawing.SystemColors.Control;
            Name = "HelpPanel";
            Size = new System.Drawing.Size(708, 390);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.RichTextBox richTextBox;
    }
}
