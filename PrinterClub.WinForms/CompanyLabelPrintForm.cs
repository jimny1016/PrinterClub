using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;
using PrinterClub.Data;
using PrinterClub.Printing;

namespace PrinterClub.WinForms
{
    public class CompanyLabelPrintForm : Form
    {
        private readonly CompanyRepository _repo;

        private ComboBox cmbPrinters;
        private TextBox txtFrom;
        private TextBox txtTo;
        private NumericUpDown nudOffsetX;
        private NumericUpDown nudOffsetY;
        private CheckBox chkUseFactoryAddress;

        private Button btnLoadList;
        private Button btnPrint;
        private Button btnClose;

        private TextBox txtLog;

        private List<CompanyLite> _selected = new();

        public CompanyLabelPrintForm(CompanyRepository repo, string? defaultFrom = null, string? defaultTo = null)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            Text = "列印 - 公司貼紙";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 520);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            BuildUi();

            txtFrom.Text = (defaultFrom ?? "").Trim();
            txtTo.Text = (defaultTo ?? "").Trim();

            LoadPrinters();
            HidePrintUntilLoaded();

            AppendLog("請選擇印表機、輸入會籍編號範圍，按「載入清單」確認要列印的貼紙。");
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            root.Controls.Add(left, 0, 0);

            int y = 10;

            Label L(string t) => new Label
            {
                Text = t,
                AutoSize = true,
                Left = 10,
                Top = y + 6
            };

            TextBox T(int width = 200) => new TextBox
            {
                Left = 140,
                Top = y,
                Width = width
            };

            // 印表機
            left.Controls.Add(L("印表機"));
            cmbPrinters = new ComboBox
            {
                Left = 140,
                Top = y,
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            left.Controls.Add(cmbPrinters);
            y += 40;

            // 範圍
            left.Controls.Add(L("會籍編號起"));
            txtFrom = T(120);
            left.Controls.Add(txtFrom);
            y += 34;

            left.Controls.Add(L("會籍編號迄"));
            txtTo = T(120);
            left.Controls.Add(txtTo);
            y += 40;

            // 地址來源
            left.Controls.Add(L("地址來源"));
            chkUseFactoryAddress = new CheckBox
            {
                Left = 140,
                Top = y + 3,
                Width = 180,
                Text = "使用工廠地址",
                Checked = false
            };
            left.Controls.Add(chkUseFactoryAddress);
            y += 40;

            // Offset
            left.Controls.Add(L("Offset X (mm)"));
            nudOffsetX = new NumericUpDown
            {
                Left = 140,
                Top = y,
                Width = 120,
                DecimalPlaces = 1,
                Minimum = -50,
                Maximum = 50,
                Increment = 0.5M
            };
            left.Controls.Add(nudOffsetX);
            y += 34;

            left.Controls.Add(L("Offset Y (mm)"));
            nudOffsetY = new NumericUpDown
            {
                Left = 140,
                Top = y,
                Width = 120,
                DecimalPlaces = 1,
                Minimum = -50,
                Maximum = 50,
                Increment = 0.5M
            };
            left.Controls.Add(nudOffsetY);
            y += 44;

            // Buttons
            btnLoadList = new Button { Text = "載入清單", Left = 10, Top = y, Width = 110, Height = 32 };
            btnPrint = new Button { Text = "開始列印", Left = 130, Top = y, Width = 110, Height = 32 };
            btnClose = new Button { Text = "關閉", Left = 250, Top = y, Width = 90, Height = 32 };

            btnLoadList.Click += (_, __) => LoadSelection();
            btnPrint.Click += (_, __) => DoPrint();
            btnClose.Click += (_, __) => Close();

            left.Controls.Add(btnLoadList);
            left.Controls.Add(btnPrint);
            left.Controls.Add(btnClose);

            // 右側 log
            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            root.Controls.Add(right, 1, 0);

            var lblLog = new Label
            {
                Text = "LOG / 本次選取清單",
                AutoSize = true,
                Left = 10,
                Top = 10
            };
            right.Controls.Add(lblLog);

            txtLog = new TextBox
            {
                Left = 10,
                Top = 34,
                Width = right.Width - 20,
                Height = right.Height - 44,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };
            right.Controls.Add(txtLog);
        }

        private void LoadPrinters()
        {
            cmbPrinters.Items.Clear();

            foreach (string p in PrinterSettings.InstalledPrinters)
                cmbPrinters.Items.Add(p);

            try
            {
                var ps = new PrinterSettings();
                var defaultName = ps.PrinterName;
                if (!string.IsNullOrWhiteSpace(defaultName))
                {
                    var idx = cmbPrinters.FindStringExact(defaultName);
                    if (idx >= 0) cmbPrinters.SelectedIndex = idx;
                }
            }
            catch
            {
            }

            if (cmbPrinters.SelectedIndex < 0 && cmbPrinters.Items.Count > 0)
                cmbPrinters.SelectedIndex = 0;
        }

        private void LoadSelection()
        {
            _selected.Clear();
            HidePrintUntilLoaded();

            var from = (txtFrom.Text ?? "").Trim();
            var to = (txtTo.Text ?? "").Trim();

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                MessageBox.Show("請輸入會籍編號範圍。", "輸入檢查", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _selected = _repo.SearchByNumberRange(from, to, 5000);

                AppendLog($"載入範圍：{from} ~ {to}");
                AppendLog($"選取筆數：{_selected.Count}");

                foreach (var c in _selected)
                    AppendLog($"- {c.Number} {c.CName}");

                if (_selected.Count == 0)
                {
                    AppendLog("查無資料。");
                    MessageBox.Show("此範圍查無資料。", "結果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                AppendLog("✅ 清單載入完成。若要列印請按「開始列印」。");
                ShowPrintAfterLoaded();
            }
            catch (Exception ex)
            {
                AppendLog("❌ 載入清單失敗：" + ex.Message);
                MessageBox.Show(ex.Message, "載入失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DoPrint()
        {
            if (_selected.Count == 0)
            {
                MessageBox.Show("請先載入清單。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                HidePrintUntilLoaded();
                return;
            }

            var printerName = cmbPrinters.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(printerName))
            {
                MessageBox.Show("請先選擇印表機。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var items = new List<CompanyLabelPrintData>(_selected.Count);

                foreach (var c in _selected)
                {
                    var full = _repo.GetByNumber(c.Number) ?? c;

                    items.Add(new CompanyLabelPrintData
                    {
                        Number = full.Number,
                        CName = full.CName,
                        CAddress = full.CAddress,
                        FAddress = full.FAddress,
                        AreaClass = full.AreaClass,
                        Chief = full.Chief,
                        Sex = full.Sex,
                        ContactPerson = full.ContactPerson,
                        UseFactoryAddress = chkUseFactoryAddress.Checked
                    });
                }

                var options = new PrintOptions
                {
                    PrinterName = printerName,
                    OffsetXmm = (float)nudOffsetX.Value,
                    OffsetYmm = (float)nudOffsetY.Value,

                    // 這裡先沿用你目前收據相近尺寸，之後可再實機微調
                    PaperWidthMm = 140f,
                    PaperHeightMm = 240f,
                    Landscape = false,

                    FontName = "標楷體",
                    FontSizePt = 12f
                };

                AppendLog("==================================");
                AppendLog("送出列印工作：公司貼紙");
                AppendLog($"印表機：{printerName}");
                AppendLog($"地址來源：{(chkUseFactoryAddress.Checked ? "工廠地址" : "公司地址")}");
                AppendLog($"Offset：X={options.OffsetXmm}mm, Y={options.OffsetYmm}mm");
                AppendLog($"筆數：{items.Count}");

                using var doc = CompanyLabelBatchPrintDocumentFactory.Create(items, options);
                doc.Print();

                AppendLog("✅ 已送出列印工作（Spool）。");

                _selected.Clear();
                HidePrintUntilLoaded();
                AppendLog("（已清空本次選取清單，若要再次列印請重新載入清單）");
            }
            catch (Exception ex)
            {
                AppendLog("❌ 列印失敗：");
                AppendLog(ex.ToString());
                MessageBox.Show(ex.ToString(), "列印失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);

                _selected.Clear();
                HidePrintUntilLoaded();
            }
        }

        private void HidePrintUntilLoaded()
        {
            btnPrint.Visible = false;
        }

        private void ShowPrintAfterLoaded()
        {
            btnPrint.Visible = true;
        }

        private void AppendLog(string msg)
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }
    }
}