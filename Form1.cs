using System;
using System.Drawing;
using System.Windows.Forms;

namespace SmartFarmUI
{
    public partial class Form1 : Form
    {
        private bool powerOn = false;
        private int currentFarm = 1;
        private Timer sensorTimer;

        public Form1()
        {
            InitializeComponent();
            InitializeSensorUI();
            InitializeLogic();
        }

        private void InitializeSensorUI()
        {
            string[] sensorNames = { "거리(습도)", "진동(온도)", "조도(압력)", "습도(공기)" };
            int yOffset = 20;

            for (int i = 0; i < sensorNames.Length; i++)
            {
                Label lblSensor = new Label
                {
                    Text = sensorNames[i],
                    Font = new Font("맑은 고딕", 10, FontStyle.Bold),
                    AutoSize = true,
                    Location = new Point(20, yOffset + i * 110)
                };

                ProgressBar bar = new ProgressBar
                {
                    Name = $"barSensor{i + 1}",
                    Size = new Size(300, 25),
                    Location = new Point(150, yOffset + i * 110),
                    Minimum = 0,
                    Maximum = 100,
                    Value = 50
                };

                Panel lamp = new Panel
                {
                    Name = $"lampSensor{i + 1}",
                    Size = new Size(25, 25),
                    Location = new Point(470, yOffset + i * 110),
                    BackColor = Color.LightGray,
                    BorderStyle = BorderStyle.FixedSingle
                };

                Label lblValue = new Label
                {
                    Name = $"lblSensorValue{i + 1}",
                    Text = "값: 50",
                    Font = new Font("맑은 고딕", 9),
                    AutoSize = true,
                    Location = new Point(530, yOffset + i * 110)
                };

                TrackBar track = new TrackBar
                {
                    Name = $"trackSensor{i + 1}",
                    Minimum = 0,
                    Maximum = 100,
                    Value = 50,
                    TickFrequency = 10,
                    Size = new Size(300, 45),
                    Location = new Point(150, yOffset + i * 110 + 30)
                };
                int index = i + 1;
                track.Scroll += (s, e) => UpdateSensor(index, track.Value);

                panelMain.Controls.Add(lblSensor);
                panelMain.Controls.Add(bar);
                panelMain.Controls.Add(lamp);
                panelMain.Controls.Add(lblValue);
                panelMain.Controls.Add(track);
            }
        }

        private void InitializeLogic()
        {
            sensorTimer = new Timer();
            sensorTimer.Interval = 3000;
            sensorTimer.Tick += SensorTimer_Tick;
            Log("시스템 준비 완료");
        }

        private void UpdateSensor(int index, int value)
        {
            var bar = panelMain.Controls[$"barSensor{index}"] as ProgressBar;
            var lamp = panelMain.Controls[$"lampSensor{index}"] as Panel;
            var lbl = panelMain.Controls[$"lblSensorValue{index}"] as Label;

            if (bar != null && lamp != null && lbl != null)
            {
                bar.Value = value;
                lbl.Text = $"값: {value}";

                int low = 30, high = 70;
                if (value < low) lamp.BackColor = Color.DeepSkyBlue;
                else if (value > high) lamp.BackColor = Color.Red;
                else lamp.BackColor = Color.LightGreen;
            }
        }

        private void btnPower_Click(object sender, EventArgs e)
        {
            powerOn = !powerOn;
            if (powerOn)
            {
                btnPower.Text = "전원 ON";
                btnPower.BackColor = Color.LightGreen;
                lblConnection.Text = "연결상태: 연결됨";
                lblConnection.ForeColor = Color.Green;
                sensorTimer.Start();
                Log("전원 켜짐");
            }
            else
            {
                btnPower.Text = "전원 OFF";
                btnPower.BackColor = Color.LightGray;
                lblConnection.Text = "연결상태: 끊김";
                lblConnection.ForeColor = Color.Red;
                sensorTimer.Stop();
                Log("전원 꺼짐");
            }
        }

        private void BtnFarm_Click(object sender, EventArgs e)
        {
            if (!powerOn) { Log("⚠️ 전원 OFF 상태에서는 스마트팜 전환 불가"); return; }
            Button btn = sender as Button;
            currentFarm = (int)btn.Tag;
            btnFarm1.BackColor = Color.WhiteSmoke;
            btnFarm2.BackColor = Color.WhiteSmoke;
            btnFarm3.BackColor = Color.WhiteSmoke;
            btn.BackColor = Color.LightGreen;
            Log($"스마트팜 {currentFarm}번 선택");
        }

        private void btnViewLog_Click(object sender, EventArgs e)
        {
            MessageBox.Show(string.Join(Environment.NewLine, GetLogEntries()), "전체 로그", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnWebConnect_Click(object sender, EventArgs e)
        {
            if (!powerOn) { Log("⚠️ 전원 OFF 상태에서는 웹 연결 불가"); return; }
            Log($"🌐 스마트팜 {currentFarm} 웹 연결 시도");
        }

        private void SensorTimer_Tick(object sender, EventArgs e)
        {
            if (!powerOn) return;

            string[] sensorNames = { "거리(습도)", "진동(온도)", "조도(압력)", "습도(공기)" };
            int low = 30, high = 70;
            bool hasAlert = false;
            string alertMsg = $"⚠️ 스마트팜 {currentFarm} 이상: ";

            for (int i = 1; i <= 4; i++)
            {
                var track = panelMain.Controls[$"trackSensor{i}"] as TrackBar;
                if (track == null) continue;

                int value = track.Value;
                UpdateSensor(i, value);

                if (value < low)
                {
                    hasAlert = true;
                    alertMsg += $"{sensorNames[i - 1]} 낮음, ";
                }
                else if (value > high)
                {
                    hasAlert = true;
                    alertMsg += $"{sensorNames[i - 1]} 높음, ";
                }
            }

            if (hasAlert)
            {
                alertMsg = alertMsg.TrimEnd(',', ' ');
                Log(alertMsg);
            }
            else
            {
                Log($"✅ 스마트팜 {currentFarm} 정상");
            }
        }

        private void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{time}] {message}";
            lstLogPreview.Items.Insert(0, logEntry);
            if (lstLogPreview.Items.Count > 50)
                lstLogPreview.Items.RemoveAt(lstLogPreview.Items.Count - 1);
        }

        private string[] GetLogEntries()
        {
            string[] logs = new string[lstLogPreview.Items.Count];
            for (int i = 0; i < lstLogPreview.Items.Count; i++)
                logs[i] = lstLogPreview.Items[i].ToString();
            return logs;
        }
    }
}
