using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SmartFarmUI
{
    public class AddCropForm : Form
    {
        private readonly TextBox txtCropName;
        private readonly TextBox txtDescription;
        private readonly TextBox txtHumidityMin;
        private readonly TextBox txtHumidityMax;
        private readonly TextBox txtTempMin;
        private readonly TextBox txtTempMax;
        private readonly TextBox txtLightMin;
        private readonly TextBox txtLightMax;
        private readonly TextBox txtSoilMin;
        private readonly TextBox txtSoilMax;
        private readonly TextBox txtProduction;
        private readonly Button btnOk;
        private readonly Button btnCancel;
        private readonly Label lblStatus;
        
        private string flaskServerUrl;
        
        public string CropName => txtCropName.Text.Trim();
        
        public AddCropForm(string flaskUrl = "http://localhost:5000")
        {
            flaskServerUrl = flaskUrl;
            
            Text = "새 작물 추가";
            Size = new Size(500, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            
            int yPos = 20;
            
            // 작물 이름
            Label lblCropName = new Label
            {
                Text = "작물 이름: *",
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            yPos += 25;
            
            txtCropName = new TextBox
            {
                Size = new Size(440, 25),
                Location = new Point(20, yPos)
            };
            yPos += 40;
            
            // 설명
            Label lblDescription = new Label
            {
                Text = "설명:",
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            yPos += 25;
            
            txtDescription = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Size = new Size(440, 60),
                Location = new Point(20, yPos),
                Text = "재배 최적 환경 조건"
            };
            yPos += 70;
            
            // 습도
            Label lblHumidity = new Label
            {
                Text = "습도 (%):",
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            
            txtHumidityMin = new TextBox
            {
                Size = new Size(80, 25),
                Location = new Point(120, yPos),
                Text = "50"
            };
            
            Label lblDash1 = new Label
            {
                Text = "~",
                AutoSize = true,
                Location = new Point(210, yPos + 5)
            };
            
            txtHumidityMax = new TextBox
            {
                Size = new Size(80, 25),
                Location = new Point(240, yPos),
                Text = "70"
            };
            yPos += 35;
            
            // 온도
            Label lblTemp = new Label
            {
                Text = "온도 (℃):",
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            
            txtTempMin = new TextBox
            {
                Size = new Size(80, 25),
                Location = new Point(120, yPos),
                Text = "20"
            };
            
            Label lblDash2 = new Label
            {
                Text = "~",
                AutoSize = true,
                Location = new Point(210, yPos + 5)
            };
            
            txtTempMax = new TextBox
            {
                Size = new Size(80, 25),
                Location = new Point(240, yPos),
                Text = "25"
            };
            yPos += 35;
            
            // 채광
            Label lblLight = new Label
            {
                Text = "채광 (%):",
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            
            txtLightMin = new TextBox
            {
                Size = new Size(80, 25),
                Location = new Point(120, yPos),
                Text = "60"
            };
            
            Label lblDash3 = new Label
            {
                Text = "~",
                AutoSize = true,
                Location = new Point(210, yPos + 5)
            };
            
            txtLightMax = new TextBox
            {
                Size = new Size(80, 25),
                Location = new Point(240, yPos),
                Text = "80"
            };
            yPos += 35;
            
            // 토양습도
            Label lblSoil = new Label
            {
                Text = "토양습도 (%):",
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            
            txtSoilMin = new TextBox
            {
                Size = new Size(80, 25),
                Location = new Point(120, yPos),
                Text = "50"
            };
            
            Label lblDash4 = new Label
            {
                Text = "~",
                AutoSize = true,
                Location = new Point(210, yPos + 5)
            };
            
            txtSoilMax = new TextBox
            {
                Size = new Size(80, 25),
                Location = new Point(240, yPos),
                Text = "70"
            };
            yPos += 35;
            
            // 생산량
            Label lblProduction = new Label
            {
                Text = "기본 생산량 (kg/식물):",
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            
            txtProduction = new TextBox
            {
                Size = new Size(100, 25),
                Location = new Point(180, yPos),
                Text = "50"
            };
            yPos += 40;
            
            // 상태 표시
            lblStatus = new Label
            {
                Text = "",
                AutoSize = true,
                Location = new Point(20, yPos),
                ForeColor = Color.Gray,
                Font = new Font("맑은 고딕", 8)
            };
            yPos += 30;
            
            // 버튼
            btnOk = new Button
            {
                Text = "추가",
                Size = new Size(90, 32),
                Location = new Point(250, yPos)
            };
            btnOk.Click += BtnOk_Click;
            
            btnCancel = new Button
            {
                Text = "취소",
                DialogResult = DialogResult.Cancel,
                Size = new Size(90, 32),
                Location = new Point(350, yPos)
            };
            
            AcceptButton = btnOk;
            CancelButton = btnCancel;
            
            Controls.Add(lblCropName);
            Controls.Add(txtCropName);
            Controls.Add(lblDescription);
            Controls.Add(txtDescription);
            Controls.Add(lblHumidity);
            Controls.Add(txtHumidityMin);
            Controls.Add(lblDash1);
            Controls.Add(txtHumidityMax);
            Controls.Add(lblTemp);
            Controls.Add(txtTempMin);
            Controls.Add(lblDash2);
            Controls.Add(txtTempMax);
            Controls.Add(lblLight);
            Controls.Add(txtLightMin);
            Controls.Add(lblDash3);
            Controls.Add(txtLightMax);
            Controls.Add(lblSoil);
            Controls.Add(txtSoilMin);
            Controls.Add(lblDash4);
            Controls.Add(txtSoilMax);
            Controls.Add(lblProduction);
            Controls.Add(txtProduction);
            Controls.Add(lblStatus);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
        }
        
        private void BtnOk_Click(object sender, EventArgs e)
        {
            // 유효성 검사
            if (string.IsNullOrWhiteSpace(txtCropName.Text))
            {
                MessageBox.Show("작물 이름을 입력해주세요.", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCropName.Focus();
                return;
            }
            
            // 숫자 값 검증
            if (!int.TryParse(txtHumidityMin.Text, out int humMin) ||
                !int.TryParse(txtHumidityMax.Text, out int humMax) ||
                !int.TryParse(txtTempMin.Text, out int tempMin) ||
                !int.TryParse(txtTempMax.Text, out int tempMax) ||
                !int.TryParse(txtLightMin.Text, out int lightMin) ||
                !int.TryParse(txtLightMax.Text, out int lightMax) ||
                !int.TryParse(txtSoilMin.Text, out int soilMin) ||
                !int.TryParse(txtSoilMax.Text, out int soilMax) ||
                !double.TryParse(txtProduction.Text, out double production))
            {
                MessageBox.Show("모든 숫자 값을 올바르게 입력해주세요.", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // 범위 검증
            if (humMin >= humMax || tempMin >= tempMax || lightMin >= lightMax || soilMin >= soilMax)
            {
                MessageBox.Show("최소값은 최대값보다 작아야 합니다.", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // Flask 서버에 작물 추가
            try
            {
                lblStatus.Text = "작물 추가 중...";
                btnOk.Enabled = false;
                
                var cropData = new Dictionary<string, object>
                {
                    ["name"] = txtCropName.Text.Trim(),
                    ["description"] = txtDescription.Text.Trim(),
                    ["base_production"] = production,
                    ["conditions"] = new Dictionary<string, object>
                    {
                        ["humidity"] = new Dictionary<string, object>
                        {
                            ["optimal_min"] = humMin,
                            ["optimal_max"] = humMax,
                            ["acceptable_min"] = Math.Max(0, humMin - 20),
                            ["acceptable_max"] = Math.Min(100, humMax + 10),
                            ["critical_min"] = Math.Max(0, humMin - 30),
                            ["critical_max"] = Math.Min(100, humMax + 20)
                        },
                        ["temperature"] = new Dictionary<string, object>
                        {
                            ["optimal_min"] = tempMin,
                            ["optimal_max"] = tempMax,
                            ["acceptable_min"] = Math.Max(-10, tempMin - 15),
                            ["acceptable_max"] = Math.Min(50, tempMax + 5),
                            ["critical_min"] = Math.Max(-10, tempMin - 20),
                            ["critical_max"] = Math.Min(50, tempMax + 10)
                        },
                        ["light"] = new Dictionary<string, object>
                        {
                            ["optimal_min"] = lightMin,
                            ["optimal_max"] = lightMax,
                            ["acceptable_min"] = Math.Max(0, lightMin - 10),
                            ["acceptable_max"] = Math.Min(100, lightMax + 10),
                            ["critical_min"] = Math.Max(0, lightMin - 30),
                            ["critical_max"] = Math.Min(100, lightMax + 20)
                        },
                        ["soil_moisture"] = new Dictionary<string, object>
                        {
                            ["optimal_min"] = soilMin,
                            ["optimal_max"] = soilMax,
                            ["acceptable_min"] = Math.Max(0, soilMin - 20),
                            ["acceptable_max"] = Math.Min(100, soilMax + 10),
                            ["critical_min"] = Math.Max(0, soilMin - 30),
                            ["critical_max"] = Math.Min(100, soilMax + 20)
                        }
                    }
                };
                
                string jsonData = JsonConvert.SerializeObject(cropData);
                
                var request = (HttpWebRequest)WebRequest.Create($"{flaskServerUrl}/api/crops");
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 5000;
                
                byte[] data = Encoding.UTF8.GetBytes(jsonData);
                request.ContentLength = data.Length;
                
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK)
                    {
                        using (var stream = response.GetResponseStream())
                        using (var reader = new System.IO.StreamReader(stream))
                        {
                            string jsonResponse = reader.ReadToEnd();
                            var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
                            
                            if (result != null && result.ContainsKey("success") && (bool)result["success"])
                            {
                                DialogResult = DialogResult.OK;
                                Close();
                                return;
                            }
                        }
                    }
                }
                
                MessageBox.Show("작물 추가에 실패했습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "작물 추가 실패";
            }
            catch (WebException ex)
            {
                string errorMessage = "서버 연결 실패";
                try
                {
                    if (ex.Response != null)
                    {
                        using (var stream = ex.Response.GetResponseStream())
                        using (var reader = new System.IO.StreamReader(stream))
                        {
                            string errorResponse = reader.ReadToEnd();
                            var errorObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(errorResponse);
                            if (errorObj != null && errorObj.ContainsKey("error"))
                            {
                                errorMessage = errorObj["error"].ToString();
                            }
                        }
                    }
                }
                catch { }
                
                MessageBox.Show($"작물 추가 실패: {errorMessage}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = $"오류: {errorMessage}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"작물 추가 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = $"오류: {ex.Message}";
            }
            finally
            {
                btnOk.Enabled = true;
            }
        }
    }
}

