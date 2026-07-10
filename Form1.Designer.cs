namespace MyLoginApp
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            webViewLogin = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)webViewLogin).BeginInit();
            SuspendLayout();
            // 
            // webViewLogin
            // 
            webViewLogin.AllowExternalDrop = true;
            webViewLogin.CreationProperties = null;
            webViewLogin.DefaultBackgroundColor = Color.White;
            webViewLogin.Dock = DockStyle.Fill;
            webViewLogin.Location = new Point(0, 0);
            webViewLogin.Name = "webViewLogin";
            webViewLogin.Size = new Size(800, 756);
            webViewLogin.TabIndex = 0;
            webViewLogin.ZoomFactor = 1D;
            webViewLogin.Click += webViewLogin_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoScroll = true;
            ClientSize = new Size(800, 756);
            Controls.Add(webViewLogin);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)webViewLogin).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Microsoft.Web.WebView2.WinForms.WebView2 webViewLogin;
    }
}
