namespace VatpacPlugin
{
    partial class VatpacWindow
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.comboBoxPage = new System.Windows.Forms.ComboBox();
            this.pages = new VatpacPlugin.VatpacPages();
            this.pageSimulator = new System.Windows.Forms.TabPage();
            this.labelSimulatorStatus = new System.Windows.Forms.Label();
            this.pages.SuspendLayout();
            this.pageSimulator.SuspendLayout();
            this.SuspendLayout();
            //
            // comboBoxPage
            //
            this.comboBoxPage.BackColor = System.Drawing.SystemColors.Window;
            this.comboBoxPage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxPage.Font = new System.Drawing.Font("Terminus (TTF)", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            this.comboBoxPage.ForeColor = System.Drawing.SystemColors.WindowText;
            this.comboBoxPage.FormattingEnabled = true;
            this.comboBoxPage.Location = new System.Drawing.Point(12, 10);
            this.comboBoxPage.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxPage.Name = "comboBoxPage";
            this.comboBoxPage.Size = new System.Drawing.Size(276, 25);
            this.comboBoxPage.TabIndex = 0;
            this.comboBoxPage.SelectedIndexChanged += new System.EventHandler(this.ComboBoxPage_SelectedIndexChanged);
            //
            // pages
            //
            this.pages.Controls.Add(this.pageSimulator);
            this.pages.Location = new System.Drawing.Point(0, 44);
            this.pages.Name = "pages";
            this.pages.SelectedIndex = 0;
            this.pages.Size = new System.Drawing.Size(300, 171);
            this.pages.TabIndex = 1;
            this.pages.TabStop = false;
            this.pages.SelectedIndexChanged += new System.EventHandler(this.Pages_SelectedIndexChanged);
            //
            // pageSimulator
            //
            this.pageSimulator.Controls.Add(this.labelSimulatorStatus);
            this.pageSimulator.Location = new System.Drawing.Point(4, 22);
            this.pageSimulator.Name = "pageSimulator";
            this.pageSimulator.Size = new System.Drawing.Size(292, 145);
            this.pageSimulator.TabIndex = 0;
            this.pageSimulator.Text = "Simulator";
            //
            // labelSimulatorStatus
            //
            this.labelSimulatorStatus.Font = new System.Drawing.Font("Terminus (TTF)", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            this.labelSimulatorStatus.Location = new System.Drawing.Point(12, 4);
            this.labelSimulatorStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelSimulatorStatus.Name = "labelSimulatorStatus";
            this.labelSimulatorStatus.Size = new System.Drawing.Size(268, 135);
            this.labelSimulatorStatus.TabIndex = 0;
            //
            // VatpacWindow
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(300, 215);
            this.Controls.Add(this.pages);
            this.Controls.Add(this.comboBoxPage);
            this.ForeColor = System.Drawing.SystemColors.InfoText;
            this.HasMinimizeButton = false;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximumSize = new System.Drawing.Size(304, 243);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(304, 243);
            this.Name = "VatpacWindow";
            this.Resizeable = false;
            this.Text = "VATPAC";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.VatpacWindow_Load);
            this.pages.ResumeLayout(false);
            this.pageSimulator.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ComboBox comboBoxPage;
        private VatpacPages pages;
        private System.Windows.Forms.TabPage pageSimulator;
        private System.Windows.Forms.Label labelSimulatorStatus;
    }
}
