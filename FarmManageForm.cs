using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SmartFarmUI
{
    public class FarmManageForm : Form
    {
        private readonly ComboBox cmbCrop;
        private readonly TextBox txtNote;
        private readonly Button btnAddCrop;
        private readonly Button btnOk;
        private readonly Button btnCancel;
        private readonly Label lblCropInfo;
        
        public string CropName => cmbCrop.Text.Trim();
        public string AdditionalNote => txtNote.Text;
        
        private List<string> availableCrops = new List<string>();
        private string flaskServerUrl;
        private Func<List<string>> getCropListFunc;
        
        public FarmManageForm(string title, string cropName, string note, string flaskUrl = "http://localhost:5000", Func<List<string>> getCropList = null)
        {
            Text = title;
            Size = new Size(520, 520);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            
            flaskServerUrl = flaskUrl;
            getCropListFunc = getCropList;
            
            Label lblCrop = new Label
            {
                Text = "재배 작물:",
                AutoSize = true,
                Location = new Point(20, 20)
            };
            
            cmbCrop = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size = new Size(300, 25),
                Location = new Point(20, 45),
                Sorted = true
            };
            
            btnAddCrop = new Button
            {
                Text = "작물 추가",
                Size = new Size(90, 25),
                Location = new Point(330, 45),
                UseVisualStyleBackColor = true
            };
            btnAddCrop.Click += BtnAddCrop_Click;
            
            lblCropInfo = new Label
            {
                Text = "",
                AutoSize = true,
                Location = new Point(20, 75),
                ForeColor = Color.Gray,
                Font = new Font("맑은 고딕", 8)
            };
            
            Label lblNote = new Label
            {
                Text = "추가 정보:",
                AutoSize = true,
                Location = new Point(20, 105)
            };
            
            txtNote = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Size = new Size(470, 290),
                Location = new Point(20, 130),
                Text = note ?? string.Empty
            };
            
            btnOk = new Button
            {
                Text = "확인",
                DialogResult = DialogResult.OK,
                Size = new Size(90, 32),
                Location = new Point(280, 435)
            };
            
            btnCancel = new Button
            {
                Text = "취소",
                DialogResult = DialogResult.Cancel,
                Size = new Size(90, 32),
                Location = new Point(380, 435)
            };
            
            AcceptButton = btnOk;
            CancelButton = btnCancel;
            
            Controls.Add(lblCrop);
            Controls.Add(cmbCrop);
            Controls.Add(btnAddCrop);
            Controls.Add(lblCropInfo);
            Controls.Add(lblNote);
            Controls.Add(txtNote);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            
            // 작물 목록 로드
            LoadCropList();
            
            // 기존 작물 선택
            if (!string.IsNullOrEmpty(cropName))
            {
                int index = cmbCrop.Items.IndexOf(cropName);
                if (index >= 0)
                {
                    cmbCrop.SelectedIndex = index;
                }
                else
                {
                    // 리스트에 없는 작물이면 첫 번째 항목 추가
                    cmbCrop.Items.Insert(0, cropName);
                    cmbCrop.SelectedIndex = 0;
                }
            }
            else if (cmbCrop.Items.Count > 0)
            {
                cmbCrop.SelectedIndex = 0;
            }
            
            cmbCrop.SelectedIndexChanged += CmbCrop_SelectedIndexChanged;
        }
        
        private void LoadCropList()
        {
            try
            {
                cmbCrop.Items.Clear();
                
                // Flask 서버에서 작물 목록 가져오기 시도
                if (getCropListFunc != null)
                {
                    try
                    {
                        availableCrops = getCropListFunc();
                    }
                    catch
                    {
                        // 함수 호출 실패 시 기본 목록 사용
                        availableCrops = GetDefaultCropList();
                    }
                }
                else
                {
                    // 기본 작물 목록 사용
                    availableCrops = GetDefaultCropList();
                }
                
                foreach (var crop in availableCrops)
                {
                    cmbCrop.Items.Add(crop);
                }
                
                lblCropInfo.Text = $"작물 {cmbCrop.Items.Count}개 로드됨";
            }
            catch (Exception ex)
            {
                lblCropInfo.Text = $"작물 목록 로드 실패: {ex.Message}";
                // 기본 목록 사용
                availableCrops = GetDefaultCropList();
                foreach (var crop in availableCrops)
                {
                    cmbCrop.Items.Add(crop);
                }
            }
        }
        
        private List<string> GetDefaultCropList()
        {
            return new List<string>
            {
                "사과", "토마토", "상추", "딸기", "오이", "고추", "배추",
                "시금치", "파프리카", "가지", "무", "브로콜리"
            };
        }
        
        private void CmbCrop_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbCrop.SelectedItem != null)
            {
                string selectedCrop = cmbCrop.SelectedItem.ToString();
                lblCropInfo.Text = $"선택: {selectedCrop}";
            }
        }
        
        private void BtnAddCrop_Click(object sender, EventArgs e)
        {
            using (var addCropForm = new AddCropForm(flaskServerUrl))
            {
                if (addCropForm.ShowDialog(this) == DialogResult.OK)
                {
                    string newCropName = addCropForm.CropName;
                    if (!string.IsNullOrEmpty(newCropName))
                    {
                        // 리스트에 추가
                        if (!cmbCrop.Items.Contains(newCropName))
                        {
                            cmbCrop.Items.Add(newCropName);
                            cmbCrop.Sorted = true;
                        }
                        
                        // 새로 추가한 작물 선택
                        cmbCrop.SelectedItem = newCropName;
                        lblCropInfo.Text = $"새 작물 추가됨: {newCropName}";
                    }
                }
            }
        }
    }
}
