using PrinterClub.Data;

namespace PrinterClub.WinForms
{
    public class RCompanyDetailForm : Form
    {
        public RCompanyLite? Result { get; private set; }
        public bool IsDeleted { get; private set; }

        private DetailFormMode _mode;
        private readonly RCompanyLite _model;

        private TextBox txtCode, txtName, txtChief;
        private TextBox txtNewsletterCopies, txtZipCode, txtAddress, txtComment;

        private Button btnEdit, btnSave, btnCancelEdit, btnDelete, btnClose;

        public RCompanyDetailForm(RCompanyLite? data, DetailFormMode mode)
        {
            _model = data ?? new RCompanyLite();
            _mode = mode;

            Text = mode == DetailFormMode.New ? "相關廠商及學校 - 新增" : "相關廠商及學校 - 詳細資料";
            StartPosition = FormStartPosition.CenterParent;

            ClientSize = new Size(920, 620);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            BuildUi();
            LoadModelToUi();
            ApplyMode(_mode);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var toolbar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 10, 10, 10) };

            btnEdit = new Button { Text = "修改", Width = 90, Height = 30, Left = 10, Top = 10 };
            btnSave = new Button { Text = "儲存", Width = 90, Height = 30, Left = 110, Top = 10 };
            btnCancelEdit = new Button { Text = "取消", Width = 90, Height = 30, Left = 210, Top = 10 };
            btnDelete = new Button { Text = "刪除", Width = 90, Height = 30, Left = 310, Top = 10 };
            btnClose = new Button { Text = "關閉", Width = 90, Height = 30, Left = 410, Top = 10 };

            btnEdit.Click += (s, e) => ApplyMode(DetailFormMode.Edit);
            btnCancelEdit.Click += (s, e) =>
            {
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
                    Result = ReadUiToModel();
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            toolbar.Controls.AddRange(new Control[] { btnEdit, btnSave, btnCancelEdit, btnDelete, btnClose });
            root.Controls.Add(toolbar, 0, 0);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };
            root.Controls.Add(scroll, 0, 1);

            var form = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4
            };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            scroll.Controls.Add(form);

            Label L(string t) => new Label { Text = t, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 10, 6, 6) };
            TextBox T(bool multi = false, int height = 26)
            {
                var tb = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(6, 6, 6, 6) };
                if (multi)
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

            txtCode = T();
            txtName = T();
            AddRow(L("代碼 *"), txtCode, L("名稱 *"), txtName);

            txtChief = T();
            txtNewsletterCopies = T();
            AddRow(L("聯絡人"), txtChief, L("會刊份數"), txtNewsletterCopies);

            txtZipCode = T();
            AddRow(L("郵遞區號"), txtZipCode, L(""), new Label { AutoSize = true });

            txtAddress = T(multi: true, height: 70);
            int rAddr = form.RowCount;
            form.RowCount += 1;
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            form.Controls.Add(L("地址"), 0, rAddr);
            form.Controls.Add(txtAddress, 1, rAddr);
            form.SetColumnSpan(txtAddress, 3);

            txtComment = T(multi: true, height: 120);
            int rCmt = form.RowCount;
            form.RowCount += 1;
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            form.Controls.Add(L("備註"), 0, rCmt);
            form.Controls.Add(txtComment, 1, rCmt);
            form.SetColumnSpan(txtComment, 3);
        }

        private void ApplyMode(DetailFormMode mode)
        {
            _mode = mode;

            bool isView = mode == DetailFormMode.View;
            bool isNew = mode == DetailFormMode.New;
            bool isEdit = mode == DetailFormMode.Edit;

            Text = isNew ? "相關廠商及學校 - 新增" : isEdit ? "相關廠商及學校 - 修改" : "相關廠商及學校 - 詳細資料";

            // code：New 可改，其餘鎖住（唯一鍵）
            txtCode.ReadOnly = !isNew;

            // 其他欄位：View readonly，Edit/New 可編
            SetEditable(!isView);

            btnEdit.Visible = isView && !isNew;
            btnSave.Visible = !isView;
            btnCancelEdit.Visible = !isView;
            btnDelete.Visible = !isNew;
            btnDelete.Enabled = isView || isEdit;

            if (isView)
                SetEditable(false);
        }

        private void SetEditable(bool editable)
        {
            void Set(TextBox tb) => tb.ReadOnly = !editable;

            Set(txtName);
            Set(txtChief);
            Set(txtNewsletterCopies);
            Set(txtZipCode);
            Set(txtAddress);
            Set(txtComment);
        }

        private void LoadModelToUi()
        {
            txtCode.Text = _model.Code;
            txtName.Text = _model.Name;
            txtChief.Text = _model.Chief;

            txtNewsletterCopies.Text = _model.NewsletterCopies;
            txtAddress.Text = _model.Address;
            txtComment.Text = _model.Comment;
            txtZipCode.Text = _model.ZipCode;
        }

        private RCompanyLite ReadUiToModel()
        {
            return new RCompanyLite
            {
                Code = (txtCode.Text ?? "").Trim(),
                Name = (txtName.Text ?? "").Trim(),
                Chief = (txtChief.Text ?? "").Trim(),
                NewsletterCopies = (txtNewsletterCopies.Text ?? "").Trim(),
                Address = (txtAddress.Text ?? "").Trim(),
                Comment = (txtComment.Text ?? "").Trim(),
                ZipCode = (txtZipCode.Text ?? "").Trim(),

                // 系統欄位：從現有值帶回（更新由 repo 決定）
                SourceLine = _model.SourceLine,
                UpdatedAt = _model.UpdatedAt,
            };
        }

        private string ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace((txtCode.Text ?? "").Trim()))
                return "代碼為必填。";
            if (string.IsNullOrWhiteSpace((txtName.Text ?? "").Trim()))
                return "名稱為必填。";
            return "";
        }
    }
}
