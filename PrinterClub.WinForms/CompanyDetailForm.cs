using PrinterClub.Data;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PrinterClub.WinForms
{
    public enum DetailFormMode
    {
        View,
        Edit,
        New
    }

    public class CompanyDetailForm : Form
    {
        // --- public result ---
        public CompanyLite? Result { get; private set; }
        public bool IsDeleted { get; private set; }

        private DetailFormMode _mode;
        private readonly CompanyLite _model;

        // controls
        private TextBox txtNumber, txtCName, txtCAddress, txtFAddress, txtTaxId, txtMoney, txtArea;
        private TextBox txtCompanyRegDate, txtCompanyRegPrefix, txtCompanyRegNo;
        private TextBox txtFactoryRegDate, txtFactoryRegPrefix, txtFactoryRegNo;
        private TextBox txtChief, txtContactPerson, txtExtension;
        private TextBox txtCTel, txtFTel, txtCFax, txtFFax, txtEmail;
        private TextBox txtMainProduct, txtEquipmentText, txtApplyDate;

        private Button btnEdit, btnSave, btnCancelEdit, btnDelete, btnClose;

        public CompanyDetailForm(CompanyLite? company, DetailFormMode mode)
        {
            _model = company ?? new CompanyLite();
            _mode = mode;

            Text = mode == DetailFormMode.New ? "Companies - 新增" : "Companies - 詳細資料";
            StartPosition = FormStartPosition.CenterParent;

            ClientSize = new Size(920, 640);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            BuildUi();
            LoadModelToUi();
            ApplyMode(_mode);
        }

        private void BuildUi()
        {
            // root: top toolbar + form content
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // --- toolbar ---
            var toolbar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 10, 10, 10) };

            btnEdit = new Button { Text = "修改", Width = 90, Height = 30, Left = 10, Top = 10 };
            btnSave = new Button { Text = "儲存", Width = 90, Height = 30, Left = 110, Top = 10 };
            btnCancelEdit = new Button { Text = "取消", Width = 90, Height = 30, Left = 210, Top = 10 };
            btnDelete = new Button { Text = "刪除", Width = 90, Height = 30, Left = 310, Top = 10 };
            btnClose = new Button { Text = "關閉", Width = 90, Height = 30, Left = 410, Top = 10 };

            btnEdit.Click += (s, e) => ApplyMode(DetailFormMode.Edit);
            btnCancelEdit.Click += (s, e) =>
            {
                // revert ui
                LoadModelToUi();
                ApplyMode(_mode == DetailFormMode.New ? DetailFormMode.New : DetailFormMode.View);
            };
            btnClose.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            btnSave.Click += (s, e) =>
            {
                var err = ValidateInputs();
                if (!string.IsNullOrEmpty(err))
                {
                    MessageBox.Show(err, "輸入檢查", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // write back
                Result = ReadUiToModel();
                DialogResult = DialogResult.OK;
                Close();
            };

            btnDelete.Click += (s, e) =>
            {
                var r = MessageBox.Show("確定要刪除這筆資料？", "刪除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r == DialogResult.Yes)
                {
                    IsDeleted = true;
                    Result = ReadUiToModel(); // 讓外部知道刪的是哪筆
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            toolbar.Controls.AddRange(new Control[] { btnEdit, btnSave, btnCancelEdit, btnDelete, btnClose });
            root.Controls.Add(toolbar, 0, 0);

            // --- content scroll ---
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };
            root.Controls.Add(scroll, 0, 1);

            // --- form layout ---
            var form = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4
            };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // label
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));  // input
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // label
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));  // input

            scroll.Controls.Add(form);

            // Helpers
            Label L(string t) => new Label { Text = t, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 10, 6, 6) };
            TextBox T(int w = 0, bool multiline = false, int height = 26)
            {
                var tb = new TextBox
                {
                    Anchor = AnchorStyles.Left | AnchorStyles.Right,
                    Margin = new Padding(6, 6, 6, 6),
                };
                if (w > 0) tb.Width = w;
                if (multiline)
                {
                    tb.Multiline = true;
                    tb.ScrollBars = ScrollBars.Vertical;
                    tb.Height = height;
                }
                return tb;
            }

            void AddRow(Control l1, Control c1, Control l2, Control c2)
            {
                int r = form.RowCount;
                form.RowCount += 1;
                form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                form.Controls.Add(l1, 0, r);
                form.Controls.Add(c1, 1, r);
                form.Controls.Add(l2, 2, r);
                form.Controls.Add(c2, 3, r);
            }

            // Row: number / apply date
            txtNumber = T();
            txtApplyDate = T();
            AddRow(L("會籍編號 *"), txtNumber, L("日期（年/月/日）"), txtApplyDate);

            // Row: company name
            txtCName = T();
            AddRow(L("公司名稱 *"), txtCName, L("地區"), txtArea = T());

            // Row: addresses
            txtCAddress = T();
            txtFAddress = T();
            AddRow(L("營業地址"), txtCAddress, L("工廠地址"), txtFAddress);

            // Row: tax/money
            txtTaxId = T();
            txtMoney = T();
            AddRow(L("統一編號"), txtTaxId, L("資本額"), txtMoney);

            // Row: 登記資訊（公司）
            txtCompanyRegDate = T();
            txtCompanyRegPrefix = T();
            txtCompanyRegNo = T();
            // 放在同一格：date / prefix / no
            var pnlCompanyReg = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            pnlCompanyReg.Controls.Add(txtCompanyRegDate);
            pnlCompanyReg.Controls.Add(new Label { Text = "字", AutoSize = true, Margin = new Padding(4, 10, 4, 6) });
            pnlCompanyReg.Controls.Add(txtCompanyRegPrefix);
            pnlCompanyReg.Controls.Add(new Label { Text = "號", AutoSize = true, Margin = new Padding(4, 10, 4, 6) });
            pnlCompanyReg.Controls.Add(txtCompanyRegNo);
            // 調整寬度
            txtCompanyRegDate.Width = 110;
            txtCompanyRegPrefix.Width = 60;
            txtCompanyRegNo.Width = 120;

            // Row: 登記資訊（工廠）
            txtFactoryRegDate = T();
            txtFactoryRegPrefix = T();
            txtFactoryRegNo = T();
            var pnlFactoryReg = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            pnlFactoryReg.Controls.Add(txtFactoryRegDate);
            pnlFactoryReg.Controls.Add(new Label { Text = "字", AutoSize = true, Margin = new Padding(4, 10, 4, 6) });
            pnlFactoryReg.Controls.Add(txtFactoryRegPrefix);
            pnlFactoryReg.Controls.Add(new Label { Text = "號", AutoSize = true, Margin = new Padding(4, 10, 4, 6) });
            pnlFactoryReg.Controls.Add(txtFactoryRegNo);
            txtFactoryRegDate.Width = 110;
            txtFactoryRegPrefix.Width = 60;
            txtFactoryRegNo.Width = 120;

            // 公司/工廠登記放成兩列
            AddRow(L("公司登記（日期/字/號）"), pnlCompanyReg, L("工廠登記（日期/字/號）"), pnlFactoryReg);

            // Row: chief/contact/ext
            txtChief = T();
            txtContactPerson = T();
            AddRow(L("負責人"), txtChief, L("聯絡人"), txtContactPerson);

            txtExtension = T();
            AddRow(L("分機"), txtExtension, L("E-mail"), txtEmail = T());

            // Row: tel/fax
            txtCTel = T();
            txtFTel = T();
            AddRow(L("公司電話"), txtCTel, L("工廠電話"), txtFTel);

            txtCFax = T();
            txtFFax = T();
            AddRow(L("傳真-公司"), txtCFax, L("傳真-工廠"), txtFFax);

            // Row: products
            txtMainProduct = T();
            AddRow(L("主要產品"), txtMainProduct, L(""), new Label { AutoSize = true });

            // Row: equipment (big)
            txtEquipmentText = T(multiline: true, height: 180);
            // equipment 佔兩欄
            int rEq = form.RowCount;
            form.RowCount += 1;
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            form.Controls.Add(L("機器設備"), 0, rEq);
            form.Controls.Add(txtEquipmentText, 1, rEq);
            form.SetColumnSpan(txtEquipmentText, 3);

            // 讓右欄 label 空白不擠
            form.Controls.Add(new Label { AutoSize = true }, 2, rEq);
        }

        private void ApplyMode(DetailFormMode mode)
        {
            // 保存模式（View/Edit/New）
            _mode = mode;

            bool isView = mode == DetailFormMode.View;
            bool isNew = mode == DetailFormMode.New;
            bool isEdit = mode == DetailFormMode.Edit;

            Text = isNew ? "Companies - 新增" : isEdit ? "Companies - 修改" : "Companies - 詳細資料";

            // number：New 可改，Edit/View 鎖住
            txtNumber.ReadOnly = !isNew;

            // 其他欄位：View 只讀，Edit/New 可編輯
            SetEditable(!isView);

            // buttons
            btnEdit.Visible = isView && !isNew;
            btnSave.Visible = !isView;
            btnCancelEdit.Visible = !isView;
            btnDelete.Visible = !isNew; // 新增不顯示刪除
            btnDelete.Enabled = isView || isEdit; // 有資料才可刪

            // View 模式：不允許刪除？（你可選）
            // 我這裡允許在 View 直接刪，但如果你要更安全：把下一行改成 isEdit 才能刪
            // btnDelete.Enabled = isEdit;

            // 如果現在是 View，確保全部 readonly
            if (isView)
            {
                SetEditable(false);
                txtEquipmentText.ReadOnly = true;
            }
        }

        private void SetEditable(bool editable)
        {
            // 除 number 外全部跟著 editable
            void Set(TextBox tb) => tb.ReadOnly = !editable;

            Set(txtCName);
            Set(txtCAddress);
            Set(txtFAddress);
            Set(txtTaxId);
            Set(txtMoney);
            Set(txtArea);

            Set(txtCompanyRegDate);
            Set(txtCompanyRegPrefix);
            Set(txtCompanyRegNo);

            Set(txtFactoryRegDate);
            Set(txtFactoryRegPrefix);
            Set(txtFactoryRegNo);

            Set(txtChief);
            Set(txtContactPerson);
            Set(txtExtension);

            Set(txtCTel);
            Set(txtFTel);
            Set(txtCFax);
            Set(txtFFax);
            Set(txtEmail);

            Set(txtMainProduct);
            Set(txtEquipmentText);
            Set(txtApplyDate);
        }

        private void LoadModelToUi()
        {
            txtNumber.Text = _model.Number;
            txtCName.Text = _model.CName;
            txtCAddress.Text = _model.CAddress;
            txtFAddress.Text = _model.FAddress;
            txtTaxId.Text = _model.TaxId;
            txtMoney.Text = _model.Money;
            txtArea.Text = _model.Area;

            txtCompanyRegDate.Text = _model.CompanyRegDate;
            txtCompanyRegPrefix.Text = _model.CompanyRegPrefix;
            txtCompanyRegNo.Text = _model.CompanyRegNo;

            txtFactoryRegDate.Text = _model.FactoryRegDate;
            txtFactoryRegPrefix.Text = _model.FactoryRegPrefix;
            txtFactoryRegNo.Text = _model.FactoryRegNo;

            txtChief.Text = _model.Chief;
            txtContactPerson.Text = _model.ContactPerson;
            txtExtension.Text = _model.Extension;

            txtCTel.Text = _model.CTel;
            txtFTel.Text = _model.FTel;
            txtCFax.Text = _model.CFax;
            txtFFax.Text = _model.FFax;
            txtEmail.Text = _model.Email;

            txtMainProduct.Text = _model.MainProduct;
            txtEquipmentText.Text = _model.EquipmentText;

            txtApplyDate.Text = _model.ApplyDate;
        }

        private CompanyLite ReadUiToModel()
        {
            // 回傳一份新的（避免外部引用同物件）
            return new CompanyLite
            {
                Number = (txtNumber.Text ?? "").Trim(),
                CName = (txtCName.Text ?? "").Trim(),
                CAddress = (txtCAddress.Text ?? "").Trim(),
                FAddress = (txtFAddress.Text ?? "").Trim(),
                TaxId = (txtTaxId.Text ?? "").Trim(),
                Money = (txtMoney.Text ?? "").Trim(),
                Area = (txtArea.Text ?? "").Trim(),

                CompanyRegDate = (txtCompanyRegDate.Text ?? "").Trim(),
                CompanyRegPrefix = (txtCompanyRegPrefix.Text ?? "").Trim(),
                CompanyRegNo = (txtCompanyRegNo.Text ?? "").Trim(),

                FactoryRegDate = (txtFactoryRegDate.Text ?? "").Trim(),
                FactoryRegPrefix = (txtFactoryRegPrefix.Text ?? "").Trim(),
                FactoryRegNo = (txtFactoryRegNo.Text ?? "").Trim(),

                Chief = (txtChief.Text ?? "").Trim(),
                ContactPerson = (txtContactPerson.Text ?? "").Trim(),
                Extension = (txtExtension.Text ?? "").Trim(),

                CTel = (txtCTel.Text ?? "").Trim(),
                FTel = (txtFTel.Text ?? "").Trim(),
                CFax = (txtCFax.Text ?? "").Trim(),
                FFax = (txtFFax.Text ?? "").Trim(),
                Email = (txtEmail.Text ?? "").Trim(),

                MainProduct = (txtMainProduct.Text ?? "").Trim(),
                EquipmentText = (txtEquipmentText.Text ?? "").Trim(),

                ApplyDate = (txtApplyDate.Text ?? "").Trim(),
            };
        }

        private string ValidateInputs()
        {
            // 你目前 UI 的必要欄位（最少集合）
            var number = (txtNumber.Text ?? "").Trim();
            var cname = (txtCName.Text ?? "").Trim();

            if (string.IsNullOrEmpty(number))
                return "會籍編號（number）為必填。";
            if (string.IsNullOrEmpty(cname))
                return "公司名稱（cname）為必填。";

            // number / taxId 可做基本格式檢查（可選）
            // if (!number.All(char.IsDigit)) return "會籍編號建議為數字。";

            return "";
        }
    }
}
