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
    public class ReceiptPrintForm : Form
    {
        private readonly CompanyRepository _repo;

        // UI
        private ComboBox cmbPrinters;
        private TextBox txtFrom;
        private TextBox txtTo;

        private TextBox txtReceiptDate;     // 列印日期（可空->今天）
        private TextBox txtReceiptNo;       // 右上角「號」
        private TextBox txtStartYm;
        private TextBox txtEndYm;

        private NumericUpDown nudFee;
        private NumericUpDown nudNewFee;

        private NumericUpDown nudOffsetX;
        private NumericUpDown nudOffsetY;

        private Button btnLoadList;
        private Button btnPrint;
        private Button btnClose;

        private TextBox txtLog;

        private List<CompanyLite> _selected = new();

        public ReceiptPrintForm(CompanyRepository repo, string? defaultFrom = null, string? defaultTo = null)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            Text = "列印 - 收據";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(980, 560);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            BuildUi();

            txtFrom.Text = (defaultFrom ?? "").Trim();
            txtTo.Text = (defaultTo ?? "").Trim();

            LoadPrinters();

            btnPrint.Visible = false; // ✅ 一開始不顯示
            AppendLog("請選擇印表機、輸入會籍編號範圍與收據資訊，按「載入清單」確認要印的會員。");
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // Left
            var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            root.Controls.Add(left, 0, 0);

            int y = 10;

            Label L(string t) => new Label { Text = t, AutoSize = true, Left = 10, Top = y + 6 };
            TextBox T(int width = 220) => new TextBox { Left = 170, Top = y, Width = width };

            // Printer
            left.Controls.Add(L("印表機"));
            cmbPrinters = new ComboBox
            {
                Left = 170,
                Top = y,
                Width = 230,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            left.Controls.Add(cmbPrinters);
            y += 40;

            // Range
            left.Controls.Add(L("會籍編號起"));
            txtFrom = T(140);
            left.Controls.Add(txtFrom);
            y += 34;

            left.Controls.Add(L("會籍編號迄"));
            txtTo = T(140);
            left.Controls.Add(txtTo);
            y += 40;

            // Receipt date
            left.Controls.Add(L("列印日期(可空=今天)"));
            txtReceiptDate = T(180);
            txtReceiptDate.PlaceholderText = "例：114.01.02 或 2026-01-02";
            left.Controls.Add(txtReceiptDate);
            y += 34;

            // Receipt No
            left.Controls.Add(L("號（右上角）"));
            txtReceiptNo = T(180);
            left.Controls.Add(txtReceiptNo);
            y += 40;

            // Start/End YM
            left.Controls.Add(L("起始年月"));
            txtStartYm = T(140);
            txtStartYm.PlaceholderText = "例：107.01";
            left.Controls.Add(txtStartYm);
            y += 34;

            left.Controls.Add(L("結束年月"));
            txtEndYm = T(140);
            txtEndYm.PlaceholderText = "例：107.12";
            left.Controls.Add(txtEndYm);
            y += 40;

            // Fee
            left.Controls.Add(L("會費"));
            nudFee = new NumericUpDown
            {
                Left = 170,
                Top = y,
                Width = 140,
                Minimum = 0,
                Maximum = 100000000,
                Increment = 100
            };
            left.Controls.Add(nudFee);
            y += 34;

            // New fee
            left.Controls.Add(L("新入會費"));
            nudNewFee = new NumericUpDown
            {
                Left = 170,
                Top = y,
                Width = 140,
                Minimum = 0,
                Maximum = 100000000,
                Increment = 100
            };
            left.Controls.Add(nudNewFee);
            y += 40;

            // Offsets
            left.Controls.Add(L("Offset X (mm)"));
            nudOffsetX = new NumericUpDown
            {
                Left = 170,
                Top = y,
                Width = 140,
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
                Left = 170,
                Top = y,
                Width = 140,
                DecimalPlaces = 1,
                Minimum = -50,
                Maximum = 50,
                Increment = 0.5M
            };
            left.Controls.Add(nudOffsetY);
            y += 44;

            // Buttons
            btnLoadList = new Button { Text = "載入清單", Left = 10, Top = y, Width = 110, Height = 34 };
            btnPrint = new Button { Text = "開始列印", Left = 130, Top = y, Width = 110, Height = 34 };
            btnClose = new Button { Text = "關閉", Left = 250, Top = y, Width = 90, Height = 34 };

            btnLoadList.Click += (_, __) => LoadSelection();
            btnPrint.Click += (_, __) => DoPrint();
            btnClose.Click += (_, __) => Close();

            left.Controls.Add(btnLoadList);
            left.Controls.Add(btnPrint);
            left.Controls.Add(btnClose);

            // Right: log
            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            root.Controls.Add(right, 1, 0);

            var lblLog = new Label { Text = "LOG / 本次選取清單", AutoSize = true, Left = 10, Top = 10 };
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
            catch { }

            if (cmbPrinters.SelectedIndex < 0 && cmbPrinters.Items.Count > 0)
                cmbPrinters.SelectedIndex = 0;
        }

        private void LoadSelection()
        {
            btnPrint.Visible = false; // ✅ 重新載入前先藏起來

            var from = (txtFrom.Text ?? "").Trim();
            var to = (txtTo.Text ?? "").Trim();

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                MessageBox.Show("請輸入會籍編號起訖（範圍列印）。", "輸入檢查", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 基本輸入檢查（不要等到 Print 才爆）
            var startYm = (txtStartYm.Text ?? "").Trim();
            var endYm = (txtEndYm.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(startYm) || string.IsNullOrWhiteSpace(endYm))
            {
                MessageBox.Show("請輸入起始年月與結束年月（例：107.01 / 107.12）。", "輸入檢查", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                AppendLog($"載入範圍：{from} ~ {to}");

                // 你已經有 SearchByNumberRange（之前比價書就用它）
                _selected = _repo.SearchByNumberRange(from, to, 5000);

                if (_selected.Count == 0)
                {
                    AppendLog("查無資料。");
                    MessageBox.Show("此範圍查無資料。", "結果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                AppendLog($"選取筆數：{_selected.Count}");
                AppendLog("清單：");
                foreach (var c in _selected)
                    AppendLog($"- {c.Number}  {c.CName}");

                AppendLog("✅ 清單載入完成。若要列印請按「開始列印」。");

                btnPrint.Visible = true; // ✅ 只有載入成功才顯示
            }
            catch (Exception ex)
            {
                AppendLog("❌ 載入清單失敗：" + ex.Message);
                MessageBox.Show(ex.Message, "載入失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DoPrint()
        {
            if (_selected == null || _selected.Count == 0)
            {
                MessageBox.Show("請先按「載入清單」確認要列印的會員。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                btnPrint.Visible = false;
                return;
            }

            var printerName = cmbPrinters.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(printerName))
            {
                MessageBox.Show("請先選擇印表機。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var receiptNo = (txtReceiptNo.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(receiptNo))
            {
                MessageBox.Show("請輸入右上角「號」。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var startYm = (txtStartYm.Text ?? "").Trim();
            var endYm = (txtEndYm.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(startYm) || string.IsNullOrWhiteSpace(endYm))
            {
                MessageBox.Show("請輸入起始年月與結束年月。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 日期可空=今天；可支援民國/西元
            DateTime printDate = DateTime.Now;
            var dateText = (txtReceiptDate.Text ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(dateText))
            {
                // 如果你 Printing 已有 DateParts.TryParseRocOrIso(string) 回 (y,m,d)
                // 這裡用簡單解析：民國/西元皆可
                printDate = ParseRocOrIsoDateToDateTime(dateText);
            }

            try
            {
                var fee = (int)nudFee.Value;
                var newFee = (int)nudNewFee.Value;

                var items = new List<ReceiptPrintData>(_selected.Count);

                foreach (var lite in _selected)
                {
                    // 收據需要 area_class（你的 CompanyLite 目前沒有）
                    // 所以建議抓 full（確保有 area_class），若你 GetByNumber 目前 select 不含 area_class，就要補欄位
                    var full = _repo.GetByNumber(lite.Number) ?? lite;

                    items.Add(new ReceiptPrintData
                    {
                        Number = full.Number,
                        CName = full.CName,
                        AreaClass = GetAreaClass(full), // 下面有 fallback

                        ReceiptNo = receiptNo,
                        PrintDate = printDate,

                        StartYm = startYm,
                        EndYm = endYm,

                        Fee = fee,
                        NewJoinFee = newFee
                    });
                }

                var options = new PrintOptions
                {
                    PrinterName = printerName,
                    OffsetXmm = (float)nudOffsetX.Value,
                    OffsetYmm = (float)nudOffsetY.Value,

                    // 收據紙：14cm x 24cm
                    PaperWidthMm = 140f,
                    PaperHeightMm = 240f,

                    FontName = "標楷體",
                    FontSizePt = 12f,
                };

                AppendLog("==================================");
                AppendLog("送出列印工作：收據");
                AppendLog($"印表機：{printerName}");
                AppendLog($"日期：{printDate:yyyy-MM-dd}（ROC={printDate.Year - 1911}）");
                AppendLog($"號：{receiptNo}");
                AppendLog($"起訖年月：{startYm} ~ {endYm}");
                AppendLog($"會費：{fee}，新入會費：{newFee}，合計：{fee + newFee}");
                AppendLog($"Offset：X={options.OffsetXmm}mm, Y={options.OffsetYmm}mm");
                AppendLog($"筆數：{items.Count}");

                var doc = ReceiptBatchPrintDocumentFactory.Create(items, options);
                doc.Print();

                AppendLog("✅ 已送出列印工作（Spool）。");

                // ✅ 列印後清空並藏起列印按鈕，避免使用者以為還是同一批
                _selected.Clear();
                btnPrint.Visible = false;
            }
            catch (Exception ex)
            {
                AppendLog("❌ 列印失敗：" + ex.Message);
                MessageBox.Show(ex.Message, "列印失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static DateTime ParseRocOrIsoDateToDateTime(string s)
        {
            s = (s ?? "").Trim();
            // 支援 114.01.02 / 114/01/02 / 2026-01-02
            char sep = s.Contains('.') ? '.' : s.Contains('/') ? '/' : '-';
            var parts = s.Split(sep);
            if (parts.Length != 3) throw new FormatException($"日期格式不正確：{s}");

            int y = int.Parse(parts[0]);
            int m = int.Parse(parts[1]);
            int d = int.Parse(parts[2]);

            if (y < 1911) y += 1911; // 民國轉西元
            return new DateTime(y, m, d);
        }

        private static string GetAreaClass(CompanyLite c)
        {
            // 你目前 CompanyLite 沒有 AreaClass，這裡先回空字串避免炸
            // 你下一步要做的是：把 CompanyLite/SQL SELECT 補上 area_class（我下面會列要改哪裡）
            var prop = c.GetType().GetProperty("AreaClass");
            if (prop == null) return "";
            return Convert.ToString(prop.GetValue(c)) ?? "";
        }

        private void AppendLog(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            txtLog.AppendText(line + Environment.NewLine);
        }
    }
}
