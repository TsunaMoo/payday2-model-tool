using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace PD2ModelParser
{
    partial class Form1
    {

        private FolderBrowserDialog folderBrowserDialog1;

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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            folderBrowserDialog1 = new FolderBrowserDialog();
            objectsTab = new TabPage();
            objectsPanel = new PD2ModelParser.UI.ObjectsPanel();
            exportTab = new TabPage();
            exportPanel1 = new PD2ModelParser.UI.ExportPanel();
            importTab = new TabPage();
            importPanel = new PD2ModelParser.UI.ImportPanel();
            mainTabs = new TabControl();
            hashTab = new TabPage();
            helpTab = new TabPage();
            helpPanel = new PD2ModelParser.UI.HelpPanel();
            hashPanel1 = new PD2ModelParser.UI.HashPanel();
            objectsTab.SuspendLayout();
            exportTab.SuspendLayout();
            importTab.SuspendLayout();
            mainTabs.SuspendLayout();
            hashTab.SuspendLayout();
            helpTab.SuspendLayout();
            SuspendLayout();
            // 
            // objectsTab
            // 
            objectsTab.BackColor = SystemColors.ControlLight;
            objectsTab.Controls.Add(objectsPanel);
            objectsTab.Location = new Point(4, 24);
            objectsTab.Margin = new Padding(4, 3, 4, 3);
            objectsTab.Name = "objectsTab";
            objectsTab.Size = new Size(776, 394);
            objectsTab.TabIndex = 3;
            objectsTab.Text = "Objects";
            // 
            // objectsPanel
            // 
            objectsPanel.BackColor = SystemColors.ControlLight;
            objectsPanel.Dock = DockStyle.Fill;
            objectsPanel.Font = new Font("Segoe UI", 9F);
            objectsPanel.ForeColor = SystemColors.ControlText;
            objectsPanel.Location = new Point(0, 0);
            objectsPanel.Margin = new Padding(5, 3, 5, 3);
            objectsPanel.Name = "objectsPanel";
            objectsPanel.Padding = new Padding(9);
            objectsPanel.Size = new Size(776, 394);
            objectsPanel.TabIndex = 0;
            // 
            // exportTab
            // 
            exportTab.BackColor = SystemColors.ControlLight;
            exportTab.Controls.Add(exportPanel1);
            exportTab.Location = new Point(4, 24);
            exportTab.Margin = new Padding(4, 3, 4, 3);
            exportTab.Name = "exportTab";
            exportTab.Padding = new Padding(4, 3, 4, 3);
            exportTab.Size = new Size(776, 394);
            exportTab.TabIndex = 1;
            exportTab.Text = "Export";
            // 
            // exportPanel1
            // 
            exportPanel1.BackColor = SystemColors.ControlLight;
            exportPanel1.Dock = DockStyle.Fill;
            exportPanel1.Font = new Font("Segoe UI", 9F);
            exportPanel1.ForeColor = SystemColors.ControlText;
            exportPanel1.Location = new Point(4, 3);
            exportPanel1.Margin = new Padding(5, 3, 5, 3);
            exportPanel1.MinimumSize = new Size(0, 157);
            exportPanel1.Name = "exportPanel1";
            exportPanel1.Padding = new Padding(9);
            exportPanel1.Size = new Size(768, 388);
            exportPanel1.TabIndex = 14;
            // 
            // importTab
            // 
            importTab.BackColor = SystemColors.ControlLight;
            importTab.Controls.Add(importPanel);
            importTab.Location = new Point(4, 24);
            importTab.Margin = new Padding(4, 3, 4, 3);
            importTab.Name = "importTab";
            importTab.Padding = new Padding(4, 3, 4, 3);
            importTab.Size = new Size(776, 394);
            importTab.TabIndex = 0;
            importTab.Text = "Import";
            // 
            // importPanel
            // 
            importPanel.BackColor = SystemColors.ControlLight;
            importPanel.Dock = DockStyle.Fill;
            importPanel.Font = new Font("Segoe UI", 9F);
            importPanel.ForeColor = SystemColors.ControlText;
            importPanel.Location = new Point(4, 3);
            importPanel.Margin = new Padding(5, 3, 5, 3);
            importPanel.MinimumSize = new Size(0, 350);
            importPanel.Name = "importPanel";
            importPanel.Padding = new Padding(9);
            importPanel.Size = new Size(768, 388);
            importPanel.TabIndex = 0;
            // 
            // mainTabs
            // 
            mainTabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            mainTabs.Controls.Add(importTab);
            mainTabs.Controls.Add(exportTab);
            mainTabs.Controls.Add(objectsTab);
            mainTabs.Controls.Add(hashTab);
            mainTabs.Controls.Add(helpTab);
            mainTabs.Location = new Point(14, 14);
            mainTabs.Margin = new Padding(4, 3, 4, 3);
            mainTabs.Name = "mainTabs";
            mainTabs.SelectedIndex = 0;
            mainTabs.Size = new Size(784, 422);
            mainTabs.TabIndex = 14;
            // 
            // hashTab
            // 
            hashTab.BackColor = SystemColors.ControlLight;
            hashTab.Controls.Add(hashPanel1);
            hashTab.Location = new Point(4, 24);
            hashTab.Name = "hashTab";
            hashTab.Padding = new Padding(3);
            hashTab.Size = new Size(776, 394);
            hashTab.TabIndex = 4;
            hashTab.Text = "Hashlist";
            // 
            // helpTab
            // 
            helpTab.Controls.Add(helpPanel);
            helpTab.Location = new Point(4, 24);
            helpTab.Name = "helpTab";
            helpTab.Padding = new Padding(3);
            helpTab.Size = new Size(776, 394);
            helpTab.TabIndex = 5;
            helpTab.Text = "Help";
            helpTab.UseVisualStyleBackColor = true;
            // 
            // helpPanel
            // 
            helpPanel.BackColor = SystemColors.ControlLight;
            helpPanel.Font = new Font("Segoe UI", 9F);
            helpPanel.ForeColor = SystemColors.ControlText;
            helpPanel.Location = new Point(0, 0);
            helpPanel.Name = "helpPanel";
            helpPanel.Padding = new Padding(8);
            helpPanel.Size = new Size(776, 395);
            helpPanel.TabIndex = 0;
            // 
            // hashPanel1
            // 
            hashPanel1.Location = new Point(0, 0);
            hashPanel1.Name = "hashPanel1";
            hashPanel1.Size = new Size(780, 403);
            hashPanel1.TabIndex = 1;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(812, 448);
            Controls.Add(mainTabs);
            Margin = new Padding(4, 3, 4, 3);
            Name = "Form1";
            ShowIcon = false;
            Text = "Diesel Model Tool v1.03";
            Load += Form1_Load;
            objectsTab.ResumeLayout(false);
            exportTab.ResumeLayout(false);
            importTab.ResumeLayout(false);
            mainTabs.ResumeLayout(false);
            hashTab.ResumeLayout(false);
            helpTab.ResumeLayout(false);
            ResumeLayout(false);

        }

        #endregion

        private TabPage objectsTab;
        private UI.ObjectsPanel objectsPanel;
        private TabPage exportTab;
        private UI.ExportPanel exportPanel1;
        private TabPage importTab;
        private UI.ImportPanel importPanel;
        private TabControl mainTabs;
        private TabPage hashTab;
        private TabPage helpTab;
        private UI.HelpPanel helpPanel;
        private UI.HashPanel hashPanel1;
    }
}