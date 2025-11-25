using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SmartFarmUI
{
    public class LogForm : Form
    {
        private TabControl tabLogs;
        private ListBox lstAllLogs;
        private ListBox lstErrorLogs;
        private ListBox lstWarningLogs;
        private ListBox lstInfoLogs;
        private Panel panelButtons;
        private Button btnSaveLogs;
        private Button btnLoadLogs;
        private Button btnClearLogs;
        
        public Func<IEnumerable<string>> GetLogs { get; set; }
        public Action<IEnumerable<string>> SetLogs { get; set; }
        public Action<string> Log { get; set; }

        public LogForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "전체 로그";
            Size = new Size(550, 480);
            StartPosition = FormStartPosition.Manual;

            // 버튼 패널
            panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(5)
            };

            btnSaveLogs = new Button
            {
                Text = "로그 저장",
                Dock = DockStyle.Left,
                Width = 100,
                Margin = new Padding(0, 0, 5, 0)
            };
            btnSaveLogs.Click += BtnSaveLogs_Click;

            btnLoadLogs = new Button
            {
                Text = "로그 불러오기",
                Dock = DockStyle.Left,
                Width = 120,
                Margin = new Padding(0, 0, 5, 0)
            };
            btnLoadLogs.Click += BtnLoadLogs_Click;

            btnClearLogs = new Button
            {
                Text = "로그 지우기",
                Dock = DockStyle.Left,
                Width = 100
            };
            btnClearLogs.Click += BtnClearLogs_Click;

            panelButtons.Controls.Add(btnSaveLogs);
            panelButtons.Controls.Add(btnLoadLogs);
            panelButtons.Controls.Add(btnClearLogs);

            tabLogs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold)
            };

            lstAllLogs = CreateLogListBox(Color.Black);
            lstErrorLogs = CreateLogListBox(Color.Red);
            lstWarningLogs = CreateLogListBox(Color.Goldenrod);
            lstInfoLogs = CreateLogListBox(Color.Black);

            tabLogs.TabPages.Add(CreateTabPage("전체 로그", lstAllLogs));
            tabLogs.TabPages.Add(CreateTabPage("오류 로그", lstErrorLogs));
            tabLogs.TabPages.Add(CreateTabPage("주의 로그", lstWarningLogs));
            tabLogs.TabPages.Add(CreateTabPage("일반 로그", lstInfoLogs));

            Controls.Add(tabLogs);
            Controls.Add(panelButtons);
        }

        private TabPage CreateTabPage(string title, ListBox listBox)
        {
            return new TabPage(title)
            {
                Padding = new Padding(6),
                Controls = { listBox }
            };
        }

        private ListBox CreateLogListBox(Color foreColor)
        {
            return new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("맑은 고딕", 9F, FontStyle.Regular),
                IntegralHeight = false,
                ForeColor = foreColor,
                BackColor = Color.White,
                HorizontalScrollbar = true
            };
        }

        public void UpdateLogs(IEnumerable<string> logs)
        {
            if (logs == null) return;

            IList<string> logList = logs as IList<string> ?? logs.ToList();

            PopulateListBox(lstAllLogs, logList);
            PopulateListBox(lstErrorLogs, logList.Where(IsErrorLog));
            PopulateListBox(lstWarningLogs, logList.Where(IsWarningLog));
            PopulateListBox(lstInfoLogs, logList.Where(IsInfoLog));
        }

        private void PopulateListBox(ListBox listBox, IEnumerable<string> logs)
        {
            listBox.BeginUpdate();
            listBox.Items.Clear();
            foreach (var log in logs.Reverse())
            {
                listBox.Items.Add(log);
            }
            listBox.EndUpdate();
        }

        private bool IsErrorLog(string log)
        {
            if (string.IsNullOrWhiteSpace(log)) return false;
            return log.Contains("⚠️") ||
                   ContainsIgnoreCase(log, "오류") ||
                   ContainsIgnoreCase(log, "에러") ||
                   ContainsIgnoreCase(log, "Error");
        }

        private bool IsWarningLog(string log)
        {
            if (string.IsNullOrWhiteSpace(log)) return false;
            return ContainsIgnoreCase(log, "경고") ||
                   ContainsIgnoreCase(log, "주의") ||
                   ContainsIgnoreCase(log, "Warning");
        }

        private bool IsInfoLog(string log)
        {
            if (string.IsNullOrWhiteSpace(log)) return false;
            return !IsErrorLog(log) && !IsWarningLog(log);
        }

        private bool ContainsIgnoreCase(string source, string value)
        {
            if (source == null || value == null) return false;
            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void BtnSaveLogs_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
                    FileName = $"SmartFarm_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                    Title = "로그 저장"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    var logs = GetLogs?.Invoke() ?? new List<string>();
                    File.WriteAllLines(saveDialog.FileName, logs, System.Text.Encoding.UTF8);
                    Log?.Invoke($"로그 저장 완료: {saveDialog.FileName} ({logs.Count()}개 항목)");
                    MessageBox.Show($"로그 저장 완료!\n파일: {saveDialog.FileName}\n항목 수: {logs.Count()}개", "저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke($"로그 저장 실패: {ex.Message}");
                MessageBox.Show($"로그 저장 중 오류가 발생했습니다.\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnLoadLogs_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog openDialog = new OpenFileDialog
                {
                    Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
                    Title = "로그 불러오기"
                };

                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    var loadedLogs = File.ReadAllLines(openDialog.FileName, System.Text.Encoding.UTF8).ToList();
                    
                    // 기존 로그에 추가할지, 교체할지 선택
                    DialogResult result = MessageBox.Show(
                        "기존 로그를 유지하고 새 로그를 추가하시겠습니까?\n\n예: 기존 로그 유지 + 새 로그 추가\n아니오: 새 로그로 교체",
                        "로그 불러오기",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Cancel) return;

                    if (result == DialogResult.Yes)
                    {
                        // 기존 로그 유지하고 추가
                        var existingLogs = GetLogs?.Invoke() ?? new List<string>();
                        var combinedLogs = existingLogs.Concat(loadedLogs).ToList();
                        SetLogs?.Invoke(combinedLogs);
                        UpdateLogs(combinedLogs);
                        Log?.Invoke($"로그 불러오기 완료 (추가): {openDialog.FileName} ({loadedLogs.Count}개 항목 추가)");
                    }
                    else
                    {
                        // 새 로그로 교체
                        SetLogs?.Invoke(loadedLogs);
                        UpdateLogs(loadedLogs);
                        Log?.Invoke($"로그 불러오기 완료 (교체): {openDialog.FileName} ({loadedLogs.Count}개 항목)");
                    }

                    MessageBox.Show($"로그 불러오기 완료!\n파일: {openDialog.FileName}\n항목 수: {loadedLogs.Count}개", "불러오기 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke($"로그 불러오기 실패: {ex.Message}");
                MessageBox.Show($"로그 불러오기 중 오류가 발생했습니다.\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnClearLogs_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "모든 로그를 지우시겠습니까?",
                "로그 지우기",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                SetLogs?.Invoke(new List<string>());
                UpdateLogs(new List<string>());
                Log?.Invoke("로그가 모두 지워졌습니다.");
            }
        }
    }
}

