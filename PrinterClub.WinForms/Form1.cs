using PrinterClub.Data;

namespace PrinterClub.WinForms
{
    public class Form1 : Form
    {
        private readonly CompanyRepository _companyRepo;
        private List<CompanyLite> _companyLastQuery = new();

        private readonly RCompanyRepository _rcompanyRepo;
        private List<RCompanyLite> _rcompanyLastQuery = new();

        private readonly TabControl tabMain;

        // Companies tab controls
        private TextBox txtCompanyNumber;
        private TextBox txtCompanyName;
        private Button btnCompanySearch;
        private Button btnCompanyClear;
        private DataGridView dgvCompanies;
        private Button btnCompanyAdd;
        private Button btnCompanyDelete;
        private Button btnCompanyPrint;

        // RCompanies tab controls
        private TextBox txtRCompanyCode;
        private TextBox txtRCompanyName;
        private Button btnRCompanySearch;
        private Button btnRCompanyClear;
        private DataGridView dgvRCompanies;
        private Button btnRCompanyAdd;
        private Button btnRCompanyDelete;
        private Button btnRCompanyPrint;

        // ====== UI 顯示字串（中文即可）======
        private readonly string menberNumberString = "會籍編號";
        private readonly string companyString = "公司名稱";
        private readonly string taxIDString = "統一編號";
        private readonly string joinDateString = "加入日期";

        private readonly string rCodeString = "代碼";
        private readonly string rNameString = "名稱";
        private readonly string rChiefString = "聯絡人";
        private readonly string rZipString = "郵遞區號";

        public Form1()
        {
            Text = "PrinterClub - 會員資料管理";
            StartPosition = FormStartPosition.CenterScreen;

            ClientSize = new Size(800, 600);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;

            tabMain = new TabControl { Dock = DockStyle.Fill };

            var tabCompanies = new TabPage("會員資料管理");
            var tabRCompanies = new TabPage("相關廠商及學校管理");

            tabMain.TabPages.Add(tabCompanies);
            tabMain.TabPages.Add(tabRCompanies);
            Controls.Add(tabMain);

            BuildCompaniesTab(tabCompanies);
            BuildRCompaniesTab(tabRCompanies);

            var dbPath = CompanyRepository.ResolveDefaultDbPath();
            _companyRepo = new CompanyRepository(dbPath);
            _rcompanyRepo = new RCompanyRepository(CompanyRepository.ResolveDefaultDbPath());

            // 初次載入：列出前 N 筆
            LoadCompaniesToGrid(_companyRepo.Search("", "", 200));
            LoadRCompaniesToGrid(_rcompanyRepo.Search("", "", 200));
        }

        private void BuildCompaniesTab(TabPage page)
        {
            page.Padding = new Padding(10);

            var grpSearch = new GroupBox
            {
                Text = "查詢",
                Dock = DockStyle.Top,
                Height = 95
            };

            var lblNumber = new Label
            {
                Text = menberNumberString,
                AutoSize = true,
                Location = new Point(12, 28)
            };

            txtCompanyNumber = new TextBox
            {
                Name = "txtCompanyNumber",
                Width = 160,
                Location = new Point(120, 24)
            };

            var lblName = new Label
            {
                Text = companyString,
                AutoSize = true,
                Location = new Point(300, 28)
            };

            txtCompanyName = new TextBox
            {
                Name = "txtCompanyName",
                Width = 240,
                Location = new Point(408, 24)
            };

            btnCompanySearch = new Button
            {
                Text = "查詢",
                Width = 90,
                Location = new Point(660, 22)
            };

            btnCompanyClear = new Button
            {
                Text = "清空",
                Width = 90,
                Location = new Point(660, 52)
            };

            txtCompanyNumber.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoCompanySearch(); };
            txtCompanyName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoCompanySearch(); };

            btnCompanySearch.Click += (s, e) => DoCompanySearch();
            btnCompanyClear.Click += (s, e) =>
            {
                txtCompanyNumber.Text = "";
                txtCompanyName.Text = "";
                DoCompanySearch();
            };

            grpSearch.Controls.Add(lblNumber);
            grpSearch.Controls.Add(txtCompanyNumber);
            grpSearch.Controls.Add(lblName);
            grpSearch.Controls.Add(txtCompanyName);
            grpSearch.Controls.Add(btnCompanySearch);
            grpSearch.Controls.Add(btnCompanyClear);

            dgvCompanies = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            dgvCompanies.Columns.Add(menberNumberString, menberNumberString);
            dgvCompanies.Columns.Add(companyString, companyString);
            dgvCompanies.Columns.Add(taxIDString, taxIDString);
            dgvCompanies.Columns.Add(joinDateString, joinDateString);

            dgvCompanies.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;

                try
                {
                    var selected = GetSelectedCompany();
                    if (selected == null)
                    {
                        MessageBox.Show("請先選取一筆資料。");
                        return;
                    }

                    var full = _companyRepo.GetByNumber(selected.Number);
                    if (full == null)
                    {
                        MessageBox.Show("資料不存在，可能已被刪除。");
                        DoCompanySearch();
                        return;
                    }

                    using var f = new CompanyDetailForm(full, DetailFormMode.View);
                    var r = f.ShowDialog(this);
                    if (r != DialogResult.OK || f.Result == null) return;

                    if (f.IsDeleted)
                        _companyRepo.Delete(f.Result.Number);
                    else
                        _companyRepo.Update(f.Result);

                    DoCompanySearch();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "修改/刪除失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var pnlActions = new Panel { Dock = DockStyle.Bottom, Height = 48 };

            btnCompanyAdd = new Button { Text = "新增", Width = 90, Location = new Point(10, 10) };
            btnCompanyDelete = new Button { Text = "刪除", Width = 90, Location = new Point(110, 10) };
            btnCompanyPrint = new Button { Text = "列印", Width = 90, Location = new Point(210, 10) };

            btnCompanyAdd.Click += (s, e) =>
            {
                try
                {
                    using var f = new CompanyDetailForm(null, DetailFormMode.New);
                    var r = f.ShowDialog(this);
                    if (r != DialogResult.OK || f.Result == null || f.IsDeleted) return;

                    _companyRepo.Insert(f.Result);
                    DoCompanySearch();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "新增失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnCompanyDelete.Click += (s, e) =>
            {
                try
                {
                    var selected = GetSelectedCompany();
                    if (selected == null)
                    {
                        MessageBox.Show("請先選取一筆資料。");
                        return;
                    }

                    var ok = MessageBox.Show($"確定刪除 {menberNumberString} = {selected.Number}？", "刪除確認",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (ok != DialogResult.Yes) return;

                    _companyRepo.Delete(selected.Number);
                    DoCompanySearch();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "刪除失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnCompanyPrint.Click += (s, e) => MessageBox.Show("TODO: 列印（會員申請表）");

            pnlActions.Controls.Add(btnCompanyAdd);
            pnlActions.Controls.Add(btnCompanyDelete);
            pnlActions.Controls.Add(btnCompanyPrint);

            page.Controls.Add(dgvCompanies);
            page.Controls.Add(pnlActions);
            page.Controls.Add(grpSearch);
        }

        private void BuildRCompaniesTab(TabPage page)
        {
            page.Padding = new Padding(10);

            var grpSearch = new GroupBox
            {
                Text = "查詢",
                Dock = DockStyle.Top,
                Height = 95
            };

            var lblCode = new Label
            {
                Text = "代碼",
                AutoSize = true,
                Location = new Point(12, 28)
            };

            txtRCompanyCode = new TextBox
            {
                Name = "txtRCompanyCode",
                Width = 160,
                Location = new Point(120, 24)
            };

            var lblName = new Label
            {
                Text = "名稱",
                AutoSize = true,
                Location = new Point(300, 28)
            };

            txtRCompanyName = new TextBox
            {
                Name = "txtRCompanyName",
                Width = 240,
                Location = new Point(408, 24)
            };

            btnRCompanySearch = new Button
            {
                Text = "查詢",
                Width = 90,
                Location = new Point(660, 22)
            };

            btnRCompanyClear = new Button
            {
                Text = "清空",
                Width = 90,
                Location = new Point(660, 52)
            };

            txtRCompanyCode.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoRCompanySearch(); };
            txtRCompanyName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoRCompanySearch(); };

            btnRCompanySearch.Click += (s, e) => DoRCompanySearch();
            btnRCompanyClear.Click += (s, e) =>
            {
                txtRCompanyCode.Text = "";
                txtRCompanyName.Text = "";
                DoRCompanySearch();
            };

            grpSearch.Controls.Add(lblCode);
            grpSearch.Controls.Add(txtRCompanyCode);
            grpSearch.Controls.Add(lblName);
            grpSearch.Controls.Add(txtRCompanyName);
            grpSearch.Controls.Add(btnRCompanySearch);
            grpSearch.Controls.Add(btnRCompanyClear);

            dgvRCompanies = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            // ✅ 列表只顯示三欄：代碼/名稱/聯絡人
            dgvRCompanies.Columns.Add("代碼", "代碼");
            dgvRCompanies.Columns.Add("名稱", "名稱");
            dgvRCompanies.Columns.Add("聯絡人", "聯絡人");

            dgvRCompanies.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;

                try
                {
                    var selected = GetSelectedRCompany();
                    if (selected == null)
                    {
                        MessageBox.Show("請先選取一筆資料。");
                        return;
                    }

                    var full = _rcompanyRepo.GetByCode(selected.Code);
                    if (full == null)
                    {
                        MessageBox.Show("資料不存在，可能已被刪除。");
                        DoRCompanySearch();
                        return;
                    }

                    using var f = new RCompanyDetailForm(full, DetailFormMode.View);
                    var r = f.ShowDialog(this);
                    if (r != DialogResult.OK || f.Result == null) return;

                    if (f.IsDeleted)
                    {
                        _rcompanyRepo.Delete(f.Result.Code);
                    }
                    else
                    {
                        _rcompanyRepo.Update(f.Result);
                    }

                    DoRCompanySearch();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "修改/刪除失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var pnlActions = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48
            };

            btnRCompanyAdd = new Button { Text = "新增", Width = 90, Location = new Point(10, 10) };
            btnRCompanyDelete = new Button { Text = "刪除", Width = 90, Location = new Point(110, 10) };
            btnRCompanyPrint = new Button { Text = "列印", Width = 90, Location = new Point(210, 10) };

            btnRCompanyAdd.Click += (s, e) =>
            {
                try
                {
                    using var f = new RCompanyDetailForm(null, DetailFormMode.New);
                    var r = f.ShowDialog(this);
                    if (r != DialogResult.OK || f.Result == null || f.IsDeleted) return;

                    _rcompanyRepo.Insert(f.Result);
                    DoRCompanySearch();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "新增失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnRCompanyDelete.Click += (s, e) =>
            {
                try
                {
                    var selected = GetSelectedRCompany();
                    if (selected == null)
                    {
                        MessageBox.Show("請先選取一筆資料。");
                        return;
                    }

                    var ok = MessageBox.Show($"確定刪除 代碼={selected.Code}？", "刪除確認",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (ok != DialogResult.Yes) return;

                    _rcompanyRepo.Delete(selected.Code);
                    DoRCompanySearch();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "刪除失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnRCompanyPrint.Click += (s, e) => MessageBox.Show("TODO: 列印（相關廠商/學校）");

            pnlActions.Controls.Add(btnRCompanyAdd);
            pnlActions.Controls.Add(btnRCompanyDelete);
            pnlActions.Controls.Add(btnRCompanyPrint);

            page.Controls.Add(dgvRCompanies);
            page.Controls.Add(pnlActions);
            page.Controls.Add(grpSearch);
        }

        private void LoadCompaniesToGrid(List<CompanyLite> list)
        {
            _companyLastQuery = list ?? new List<CompanyLite>();

            dgvCompanies.Rows.Clear();

            foreach (var c in _companyLastQuery)
            {
                dgvCompanies.Rows.Add(c.Number, c.CName, c.TaxId, c.ApplyDate);
            }
        }

        private CompanyLite? GetSelectedCompany()
        {
            if (dgvCompanies.CurrentRow == null) return null;
            var idx = dgvCompanies.CurrentRow.Index;
            if (idx < 0 || idx >= _companyLastQuery.Count) return null;
            return _companyLastQuery[idx];
        }

        private void DoCompanySearch()
        {
            try
            {
                var number = (txtCompanyNumber.Text ?? "").Trim();
                var cname = (txtCompanyName.Text ?? "").Trim();

                if (!string.IsNullOrEmpty(number))
                {
                    var list = _companyRepo.Search(number, "", 200);
                    LoadCompaniesToGrid(list);
                    return;
                }

                var result = _companyRepo.Search("", cname, 200);
                LoadCompaniesToGrid(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "查詢失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadRCompaniesToGrid(List<RCompanyLite> list)
        {
            _rcompanyLastQuery = list ?? new List<RCompanyLite>();

            dgvRCompanies.Rows.Clear();
            foreach (var r in _rcompanyLastQuery)
            {
                dgvRCompanies.Rows.Add(r.Code, r.Name, r.Chief);
            }
        }

        private RCompanyLite? GetSelectedRCompany()
        {
            if (dgvRCompanies.CurrentRow == null) return null;
            var idx = dgvRCompanies.CurrentRow.Index;
            if (idx < 0 || idx >= _rcompanyLastQuery.Count) return null;
            return _rcompanyLastQuery[idx];
        }

        private void DoRCompanySearch()
        {
            try
            {
                var code = (txtRCompanyCode.Text ?? "").Trim();
                var name = (txtRCompanyName.Text ?? "").Trim();

                if (!string.IsNullOrEmpty(code))
                {
                    LoadRCompaniesToGrid(_rcompanyRepo.Search(code, "", 200));
                    return;
                }

                LoadRCompaniesToGrid(_rcompanyRepo.Search("", name, 200));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "查詢失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
