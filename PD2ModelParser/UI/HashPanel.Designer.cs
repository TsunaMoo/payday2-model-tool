namespace PD2ModelParser.UI
{
    partial class HashPanel
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
            label1 = new System.Windows.Forms.Label();
            checkBox = new System.Windows.Forms.CheckBox();
            textBox1 = new System.Windows.Forms.TextBox();
            updateButton = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(20, 26);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(91, 15);
            label1.TabIndex = 7;
            label1.Text = "Hashlist Source:";
            // 
            // checkBox
            // 
            checkBox.AutoSize = true;
            checkBox.Checked = true;
            checkBox.CheckState = System.Windows.Forms.CheckState.Checked;
            checkBox.Location = new System.Drawing.Point(135, 58);
            checkBox.Name = "checkBox";
            checkBox.Size = new System.Drawing.Size(141, 19);
            checkBox.TabIndex = 6;
            checkBox.Text = "Update Automatically";
            checkBox.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            checkBox.UseVisualStyleBackColor = true;
            // 
            // textBox1
            // 
            textBox1.Location = new System.Drawing.Point(135, 23);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(615, 23);
            textBox1.TabIndex = 5;
            textBox1.Text = "https://raw.githubusercontent.com/Luffyyy/PAYDAY-2-Hashlist/refs/heads/master/hashlist";
            // 
            // updateButton
            // 
            updateButton.Location = new System.Drawing.Point(20, 55);
            updateButton.Name = "updateButton";
            updateButton.Size = new System.Drawing.Size(91, 23);
            updateButton.TabIndex = 4;
            updateButton.Text = "Fetch";
            updateButton.UseVisualStyleBackColor = true;
            // 
            // HashPanel
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(label1);
            Controls.Add(checkBox);
            Controls.Add(textBox1);
            Controls.Add(updateButton);
            Name = "HashPanel";
            Size = new System.Drawing.Size(772, 403);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox checkBox;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button updateButton;
    }
}
