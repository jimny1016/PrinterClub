using PrinterClub.Data;
using System.Collections.Generic;

namespace PrinterClub.WinForms
{

    public class Form1 : Form
    {
        private readonly CompanyRepository _companyRepo;
        private List<CompanyLite> _companyLastQuery = new();

        private readonly TabControl tabMain;

        // Companies tab controls
        private TextBox txtCompanyNumber;
        private TextBox txtCompanyName;
        private Button btnCompanySearch;
        private Button btnCompanyClear;
        private DataGridView dgvCompanies;
        private Button btnCompanyAdd;
        private Button btnCompanyEdit;
        private Button btnCompanyDelete;
        private Button btnCompanyPrint;

        // RCompanies tab controls
        private TextBox txtRCompanyCode;
        private TextBox txtRCompanyName;
        private Button btnRCompanySearch;
        private Button btnRCompanyClear;
        private DataGridView dgvRCompanies;
        private Button btnRCompanyAdd;
        private Button btnRCompanyEdit;
        private Button btnRCompanyDelete;
        private Button btnRCompanyPrint;

        public Form1()
        {
            // ===== Window =====
            Text = "PrinterClub - 會員資料管理";
            StartPosition = FormStartPosition.CenterScreen;

            // 固定尺寸：你先用 800x600（之後想 FullHD 再調）
            ClientSize = new Size(800, 600);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;

            // ===== Tabs =====
            tabMain = new TabControl
            {
                Dock = DockStyle.Fill
            };

            var tabCompanies = new TabPage("Companies（會員）");
            var tabRCompanies = new TabPage("RCompanies（名冊/其他）");

            tabMain.TabPages.Add(tabCompanies);
            tabMain.TabPages.Add(tabRCompanies);

            Controls.Add(tabMain);

            BuildCompaniesTab(tabCompanies);
            BuildRCompaniesTab(tabRCompanies);

            _companyRepo = new CompanyRepository(CompanyRepository.ResolveDefaultDbPath());

            // 初次載入：列出前 N 筆
            LoadCompaniesToGrid(_companyRepo.Search("", "", 200));
        }

        private void BuildCompaniesTab(TabPage page)
        {
            page.Padding = new Padding(10);

            // 上半部：查詢條件
            var grpSearch = new GroupBox
            {
                Text = "查詢條件（Companies）",
                Dock = DockStyle.Top,
                Height = 95
            };

            var lblNumber = new Label
            {
                Text = "number（精準）",
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
                Text = "cname（模糊）",
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

            // Enter 直接查詢
            txtCompanyNumber.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoCompanySearch(); };
            txtCompanyName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoCompanySearch(); };

            btnCompanySearch.Click += (s, e) => DoCompanySearch();
            btnCompanyClear.Click += (s, e) =>
            {
                txtCompanyNumber.Text = "";
                txtCompanyName.Text = "";
                // TODO: 你之後接資料庫後，這裡也可以刷新列表
            };

            grpSearch.Controls.Add(lblNumber);
            grpSearch.Controls.Add(txtCompanyNumber);
            grpSearch.Controls.Add(lblName);
            grpSearch.Controls.Add(txtCompanyName);
            grpSearch.Controls.Add(btnCompanySearch);
            grpSearch.Controls.Add(btnCompanyClear);

            // 中間：列表
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

            // 先放假欄位（之後接 DB 再換）
            dgvCompanies.Columns.Add("number", "number");
            dgvCompanies.Columns.Add("cname", "cname");
            dgvCompanies.Columns.Add("tax_id", "tax_id");
            dgvCompanies.Columns.Add("join_date", "join_date");
            dgvCompanies.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return; // header
                btnCompanyEdit.PerformClick(); // 直接沿用修改的流程（先 View）
            };


            // 下半部：按鈕列
            var pnlActions = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48
            };

            btnCompanyAdd = new Button { Text = "新增", Width = 90, Location = new Point(10, 10) };
            btnCompanyEdit = new Button { Text = "修改", Width = 90, Location = new Point(110, 10) };
            btnCompanyDelete = new Button { Text = "刪除", Width = 90, Location = new Point(210, 10) };
            btnCompanyPrint = new Button { Text = "列印", Width = 90, Location = new Point(310, 10) };

            // 先接 placeholder
            btnCompanyAdd.Click += (s, e) =>
            {
                try
                {
                    using var f = new CompanyDetailForm(null, DetailFormMode.New);
                    var r = f.ShowDialog(this);
                    if (r != DialogResult.OK || f.Result == null || f.IsDeleted) return;

                    _companyRepo.Insert(f.Result);
                    DoCompanySearch(); // 重新查詢刷新（會依目前搜尋條件）
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "新增失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnCompanyEdit.Click += (s, e) =>
            {
                try
                {
                    var selected = GetSelectedCompany();
                    if (selected == null)
                    {
                        MessageBox.Show("請先選取一筆資料。");
                        return;
                    }

                    // 重新從 DB 讀完整資料（避免列表欄位太少）
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
                    {
                        _companyRepo.Delete(f.Result.Number);
                    }
                    else
                    {
                        _companyRepo.Update(f.Result);
                    }

                    DoCompanySearch();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "修改/刪除失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                    var ok = MessageBox.Show($"確定刪除 number={selected.Number}？", "刪除確認",
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

            btnCompanyPrint.Click += (s, e) => MessageBox.Show("TODO: 列印 Companies");

            pnlActions.Controls.Add(btnCompanyAdd);
            pnlActions.Controls.Add(btnCompanyEdit);
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
                Text = "查詢條件（RCompanies）",
                Dock = DockStyle.Top,
                Height = 95
            };

            var lblCode = new Label
            {
                Text = "code（精準）",
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
                Text = "name（模糊）",
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

            dgvRCompanies.Columns.Add("code", "code");
            dgvRCompanies.Columns.Add("name", "name");
            dgvRCompanies.Columns.Add("chief", "chief");
            dgvRCompanies.Columns.Add("zip_code", "zip_code");

            var pnlActions = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48
            };

            btnRCompanyAdd = new Button { Text = "新增", Width = 90, Location = new Point(10, 10) };
            btnRCompanyEdit = new Button { Text = "修改", Width = 90, Location = new Point(110, 10) };
            btnRCompanyDelete = new Button { Text = "刪除", Width = 90, Location = new Point(210, 10) };
            btnRCompanyPrint = new Button { Text = "列印", Width = 90, Location = new Point(310, 10) };

            btnRCompanyAdd.Click += (s, e) => MessageBox.Show("TODO: 新增 RCompanies");
            btnRCompanyEdit.Click += (s, e) => MessageBox.Show("TODO: 修改 RCompanies");
            btnRCompanyDelete.Click += (s, e) => MessageBox.Show("TODO: 刪除 RCompanies");
            btnRCompanyPrint.Click += (s, e) => MessageBox.Show("TODO: 列印 RCompanies");

            pnlActions.Controls.Add(btnRCompanyAdd);
            pnlActions.Controls.Add(btnRCompanyEdit);
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
            if (dgvCompanies.Columns.Count == 0)
            {
                dgvCompanies.Columns.Add("number", "number");
                dgvCompanies.Columns.Add("cname", "cname");
                dgvCompanies.Columns.Add("tax_id", "tax_id");
                dgvCompanies.Columns.Add("join_date", "join_date");
            }

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

        // ===== Placeholder search handlers (UI 拉皮先做行為規則) =====
        private void DoCompanySearch()
        {
            try
            {
                var number = (txtCompanyNumber.Text ?? "").Trim();
                var cname = (txtCompanyName.Text ?? "").Trim();

                // 規則：若同時輸入，優先 number
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

        private void DoRCompanySearch()
        {
            // 規則：code 精準、name 模糊
            var code = (txtRCompanyCode.Text ?? "").Trim();
            var name = (txtRCompanyName.Text ?? "").Trim();

            if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(name))
            {
                MessageBox.Show("建議：code 與 name 同時輸入時，以 code 精準為主（或你也可以做 AND 搜尋）。\n目前先做 UI，資料庫稍後接。");
            }
            else if (!string.IsNullOrEmpty(code))
            {
                MessageBox.Show($"TODO: 查詢 RCompanies by code = '{code}'");
            }
            else if (!string.IsNullOrEmpty(name))
            {
                MessageBox.Show($"TODO: 查詢 RCompanies by name LIKE '%{name}%'");
            }
            else
            {
                MessageBox.Show("TODO: 不帶條件 -> 列出前 N 筆 RCompanies（或全部）");
            }
        }

        private void OpenCompanyDetailView(CompanyLite company)
        {
            using var f = new CompanyDetailForm(company, DetailFormMode.View);
            var r = f.ShowDialog(this);

            if (r == DialogResult.OK)
            {
                if (f.IsDeleted)
                {
                    // TODO: delete by f.Result.Number
                }
                else if (f.Result != null)
                {
                    // 若你允許 View 直接刪/或 View 先按修改再存，這裡會拿到更新後資料
                    // TODO: update f.Result
                }

                // TODO: refresh grid
            }
        }
        private void OpenCompanyDetailNew()
        {
            using var f = new CompanyDetailForm(null, DetailFormMode.New);
            var r = f.ShowDialog(this);

            if (r == DialogResult.OK && f.Result != null && !f.IsDeleted)
            {
                // TODO: insert f.Result (number unique)
                // TODO: refresh grid
            }
        }

    }
}
