namespace SmartFarmUI
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.panelRight = new System.Windows.Forms.Panel();
            this.grpBottomButtons = new System.Windows.Forms.GroupBox();
            this.btnViewLog = new System.Windows.Forms.Button();
            this.btnWebConnect = new System.Windows.Forms.Button();
            this.grpLog = new System.Windows.Forms.GroupBox();
            this.lstLogPreview = new System.Windows.Forms.ListBox();
            this.grpFarm = new System.Windows.Forms.GroupBox();
            this.btnFarm1 = new System.Windows.Forms.Button();
            this.btnFarm2 = new System.Windows.Forms.Button();
            this.btnFarm3 = new System.Windows.Forms.Button();
            this.grpSystem = new System.Windows.Forms.GroupBox();
            this.btnPower = new System.Windows.Forms.Button();
            this.lblConnection = new System.Windows.Forms.Label();
            this.panelMain = new System.Windows.Forms.Panel();
            this.panelRight.SuspendLayout();
            this.grpBottomButtons.SuspendLayout();
            this.grpLog.SuspendLayout();
            this.grpFarm.SuspendLayout();
            this.grpSystem.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelRight
            // 
            this.panelRight.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(250)))));
            this.panelRight.Controls.Add(this.grpBottomButtons);
            this.panelRight.Controls.Add(this.grpLog);
            this.panelRight.Controls.Add(this.grpFarm);
            this.panelRight.Controls.Add(this.grpSystem);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Right;
            this.panelRight.Location = new System.Drawing.Point(580, 0);
            this.panelRight.Name = "panelRight";
            this.panelRight.Size = new System.Drawing.Size(320, 600);
            this.panelRight.TabIndex = 0;
            // 
            // grpBottomButtons
            // 
            this.grpBottomButtons.Controls.Add(this.btnViewLog);
            this.grpBottomButtons.Controls.Add(this.btnWebConnect);
            this.grpBottomButtons.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold);
            this.grpBottomButtons.Location = new System.Drawing.Point(10, 510);
            this.grpBottomButtons.Name = "grpBottomButtons";
            this.grpBottomButtons.Size = new System.Drawing.Size(300, 80);
            this.grpBottomButtons.TabIndex = 0;
            this.grpBottomButtons.TabStop = false;
            // 
            // btnViewLog
            // 
            this.btnViewLog.Location = new System.Drawing.Point(10, 20);
            this.btnViewLog.Name = "btnViewLog";
            this.btnViewLog.Size = new System.Drawing.Size(140, 40);
            this.btnViewLog.TabIndex = 0;
            this.btnViewLog.Text = "전체 로그 보기";
            this.btnViewLog.Click += new System.EventHandler(this.btnViewLog_Click);
            // 
            // btnWebConnect
            // 
            this.btnWebConnect.Location = new System.Drawing.Point(150, 20);
            this.btnWebConnect.Name = "btnWebConnect";
            this.btnWebConnect.Size = new System.Drawing.Size(140, 40);
            this.btnWebConnect.TabIndex = 1;
            this.btnWebConnect.Text = "웹 연결";
            this.btnWebConnect.Click += new System.EventHandler(this.btnWebConnect_Click);
            // 
            // grpLog
            // 
            this.grpLog.Controls.Add(this.lstLogPreview);
            this.grpLog.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold);
            this.grpLog.Location = new System.Drawing.Point(10, 300);
            this.grpLog.Name = "grpLog";
            this.grpLog.Size = new System.Drawing.Size(300, 210);
            this.grpLog.TabIndex = 1;
            this.grpLog.TabStop = false;
            this.grpLog.Text = "로그 요약";
            // 
            // lstLogPreview
            // 
            this.lstLogPreview.HorizontalScrollbar = true;
            this.lstLogPreview.ItemHeight = 17;
            this.lstLogPreview.Location = new System.Drawing.Point(10, 24);
            this.lstLogPreview.Name = "lstLogPreview";
            this.lstLogPreview.Size = new System.Drawing.Size(280, 174);
            this.lstLogPreview.TabIndex = 0;
            // 
            // grpFarm
            // 
            this.grpFarm.Controls.Add(this.btnFarm1);
            this.grpFarm.Controls.Add(this.btnFarm2);
            this.grpFarm.Controls.Add(this.btnFarm3);
            this.grpFarm.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold);
            this.grpFarm.Location = new System.Drawing.Point(10, 120);
            this.grpFarm.Name = "grpFarm";
            this.grpFarm.Size = new System.Drawing.Size(300, 180);
            this.grpFarm.TabIndex = 2;
            this.grpFarm.TabStop = false;
            this.grpFarm.Text = "스마트팜 선택";
            // 
            // btnFarm1
            // 
            this.btnFarm1.BackColor = System.Drawing.Color.WhiteSmoke;
            this.btnFarm1.Location = new System.Drawing.Point(15, 30);
            this.btnFarm1.Name = "btnFarm1";
            this.btnFarm1.Size = new System.Drawing.Size(260, 40);
            this.btnFarm1.TabIndex = 0;
            this.btnFarm1.Tag = 1;
            this.btnFarm1.Text = "스마트팜 1";
            this.btnFarm1.UseVisualStyleBackColor = false;
            this.btnFarm1.Click += new System.EventHandler(this.BtnFarm_Click);
            // 
            // btnFarm2
            // 
            this.btnFarm2.BackColor = System.Drawing.Color.WhiteSmoke;
            this.btnFarm2.Location = new System.Drawing.Point(15, 75);
            this.btnFarm2.Name = "btnFarm2";
            this.btnFarm2.Size = new System.Drawing.Size(260, 40);
            this.btnFarm2.TabIndex = 1;
            this.btnFarm2.Tag = 2;
            this.btnFarm2.Text = "스마트팜 2";
            this.btnFarm2.UseVisualStyleBackColor = false;
            this.btnFarm2.Click += new System.EventHandler(this.BtnFarm_Click);
            // 
            // btnFarm3
            // 
            this.btnFarm3.BackColor = System.Drawing.Color.WhiteSmoke;
            this.btnFarm3.Location = new System.Drawing.Point(15, 120);
            this.btnFarm3.Name = "btnFarm3";
            this.btnFarm3.Size = new System.Drawing.Size(260, 40);
            this.btnFarm3.TabIndex = 2;
            this.btnFarm3.Tag = 3;
            this.btnFarm3.Text = "스마트팜 3";
            this.btnFarm3.UseVisualStyleBackColor = false;
            this.btnFarm3.Click += new System.EventHandler(this.BtnFarm_Click);
            // 
            // grpSystem
            // 
            this.grpSystem.Controls.Add(this.btnPower);
            this.grpSystem.Controls.Add(this.lblConnection);
            this.grpSystem.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold);
            this.grpSystem.Location = new System.Drawing.Point(10, 10);
            this.grpSystem.Name = "grpSystem";
            this.grpSystem.Size = new System.Drawing.Size(300, 100);
            this.grpSystem.TabIndex = 3;
            this.grpSystem.TabStop = false;
            this.grpSystem.Text = "시스템 상태";
            // 
            // btnPower
            // 
            this.btnPower.BackColor = System.Drawing.Color.LightGray;
            this.btnPower.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnPower.Location = new System.Drawing.Point(15, 30);
            this.btnPower.Name = "btnPower";
            this.btnPower.Size = new System.Drawing.Size(90, 40);
            this.btnPower.TabIndex = 0;
            this.btnPower.Text = "전원 OFF";
            this.btnPower.UseVisualStyleBackColor = false;
            this.btnPower.Click += new System.EventHandler(this.btnPower_Click);
            // 
            // lblConnection
            // 
            this.lblConnection.AutoSize = true;
            this.lblConnection.ForeColor = System.Drawing.Color.Red;
            this.lblConnection.Location = new System.Drawing.Point(120, 40);
            this.lblConnection.Name = "lblConnection";
            this.lblConnection.Size = new System.Drawing.Size(102, 19);
            this.lblConnection.TabIndex = 1;
            this.lblConnection.Text = "연결상태: 끊김";
            // 
            // panelMain
            // 
            this.panelMain.BackColor = System.Drawing.Color.WhiteSmoke;
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 0);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(900, 600);
            this.panelMain.TabIndex = 1;
            // 
            // Form1
            // 
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(250)))));
            this.ClientSize = new System.Drawing.Size(900, 600);
            this.Controls.Add(this.panelRight);
            this.Controls.Add(this.panelMain);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "스마트팜 제어 시스템";
            this.panelRight.ResumeLayout(false);
            this.grpBottomButtons.ResumeLayout(false);
            this.grpLog.ResumeLayout(false);
            this.grpFarm.ResumeLayout(false);
            this.grpSystem.ResumeLayout(false);
            this.grpSystem.PerformLayout();
            this.ResumeLayout(false);

        }
        #endregion

        private System.Windows.Forms.Panel panelRight;
        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.GroupBox grpSystem;
        private System.Windows.Forms.Button btnPower;
        private System.Windows.Forms.Label lblConnection;
        private System.Windows.Forms.GroupBox grpFarm;
        private System.Windows.Forms.Button btnFarm1;
        private System.Windows.Forms.Button btnFarm2;
        private System.Windows.Forms.Button btnFarm3;
        private System.Windows.Forms.GroupBox grpLog;
        private System.Windows.Forms.ListBox lstLogPreview;
        private System.Windows.Forms.GroupBox grpBottomButtons;
        private System.Windows.Forms.Button btnViewLog;
        private System.Windows.Forms.Button btnWebConnect;
    }
}
