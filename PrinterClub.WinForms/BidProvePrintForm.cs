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
    public class BidProvePrintForm : Form
    {
        private readonly CompanyRepository _repo;

        // UI
        private ComboBox cmbPrinters;
        private TextBox txtFrom;
        private TextBox txtTo;
        private TextBox txtValidDate;

        private NumericUpDown nudOffsetX;
        private NumericUpDown nudOffsetY;

        private Button btnLoadList;
        private Button btnPrint;
        private Button btnClose;

        private TextBox txtLog;

        // data prepared for printing
        private List<CompanyLite> _selected = new();

        public BidProvePrintForm(CompanyRepository repo, string? defaultFrom = null, string? defaultTo = null)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            Text = "列印 - 比價證明書";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 520);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            BuildUi();

            txtFrom.Text = (defaultFrom ?? "").Trim();
            txtTo.Text = (defaultTo ?? "").Trim();

            LoadPrinters();

            // 一開始不顯示開始列印（避免疑惑）
            HidePrintUntilLoaded();

            AppendLog("請選擇印表機、輸入會籍編號範圍與比價證明書有效日期，按「載入清單」確認要印的會員。");
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

            // Left panel: inputs
            var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            root.Controls.Add(left, 0, 0);

            int y = 10;

            Label L(string t) => new Label { Text = t, AutoSize = true, Left = 10, Top = y + 6 };
            TextBox T(int width = 240)
            {
                return new TextBox { Left = 140, Top = y, Width = width };
            }

            // Printer
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

            // Range
            left.Controls.Add(L("會籍編號起"));
            txtFrom = T(120);
            left.Controls.Add(txtFrom);
            y += 34;

            left.Controls.Add(L("會籍編號迄"));
            txtTo = T(120);
            left.Controls.Add(txtTo);
            y += 34;

            // Valid date
            left.Controls.Add(L("有效日期"));
            txtValidDate = T(160);
            txtValidDate.PlaceholderText = "例：107.12.31";
            left.Controls.Add(txtValidDate);
            y += 40;

            // Offsets
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

            // Right panel: log textarea
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

            // ====== 防呆：任何輸入變更，都要讓使用者重新載入清單 ======
            txtFrom.TextChanged += (_, __) => InvalidateSelection("會籍編號範圍已變更，請重新載入清單。");
            txtTo.TextChanged += (_, __) => InvalidateSelection("會籍編號範圍已變更，請重新載入清單。");
            txtValidDate.TextChanged += (_, __) => InvalidateSelection("有效日期已變更，請重新載入清單。");
            nudOffsetX.ValueChanged += (_, __) => InvalidateSelection("Offset 已變更，請重新載入清單。");
            nudOffsetY.ValueChanged += (_, __) => InvalidateSelection("Offset 已變更，請重新載入清單。");
            cmbPrinters.SelectedIndexChanged += (_, __) => InvalidateSelection("印表機已變更，請重新載入清單。");
        }

        private void LoadPrinters()
        {
            cmbPrinters.Items.Clear();

            foreach (string p in PrinterSettings.InstalledPrinters)
                cmbPrinters.Items.Add(p);

            // 預設印表機（如果可取得）
            try
            {
                var ps = new System.Drawing.Printing.PrinterSettings();
                var defaultName = ps.PrinterName;
                if (!string.IsNullOrWhiteSpace(defaultName))
                {
                    var idx = cmbPrinters.FindStringExact(defaultName);
                    if (idx >= 0) cmbPrinters.SelectedIndex = idx;
                }
            }
            catch { /* ignore */ }

            if (cmbPrinters.SelectedIndex < 0 && cmbPrinters.Items.Count > 0)
                cmbPrinters.SelectedIndex = 0;
        }

        private void LoadSelection()
        {
            var from = (txtFrom.Text ?? "").Trim();
            var to = (txtTo.Text ?? "").Trim();

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                MessageBox.Show("請輸入會籍編號起訖（範圍列印）。", "輸入檢查", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // 每次載入都先清掉舊的
                ClearSelectionAndHidePrint(silent: true);

                AppendLog($"載入範圍：{from} ~ {to}");
                _selected = _repo.SearchByNumberRange(from, to, 5000);

                if (_selected.Count == 0)
                {
                    AppendLog("查無資料。");
                    MessageBox.Show("此範圍查無資料。", "結果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    HidePrintUntilLoaded();
                    return;
                }

                AppendLog($"選取筆數：{_selected.Count}");
                AppendLog("清單：");
                foreach (var c in _selected)
                    AppendLog($"- {c.Number}  {c.CName}");

                AppendLog("✅ 清單載入完成。若要列印請按「開始列印」。");

                // ✅ 成功載入後才顯示開始列印
                ShowPrintAfterLoaded();
            }
            catch (Exception ex)
            {
                AppendLog("❌ 載入清單失敗：" + ex.Message);
                MessageBox.Show(ex.Message, "載入失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                HidePrintUntilLoaded();
            }
        }

        private void DoPrint()
        {
            // 理論上按鈕不可見時也按不到，但多一道保護
            if (_selected == null || _selected.Count == 0)
            {
                MessageBox.Show("請先按「載入清單」確認要列印的會員。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                HidePrintUntilLoaded();
                return;
            }

            var printerName = cmbPrinters.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(printerName))
            {
                MessageBox.Show("請先選擇印表機。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var validDate = (txtValidDate.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(validDate))
            {
                MessageBox.Show("請輸入比價證明書有效日期（例：107.12.31）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // 轉成 printing data
                var items = new List<BidProvePrintData>(_selected.Count);

                foreach (var lite in _selected)
                {
                    var full = _repo.GetByNumber(lite.Number) ?? lite;

                    items.Add(new BidProvePrintData
                    {
                        Number = full.Number,
                        CName = full.CName,
                        Money = full.Money,
                        FAddress = full.FAddress,

                        Title = full.Title,
                        Chief = full.Chief,
                        Sex = full.Sex,

                        FactoryRegPrefix = full.FactoryRegPrefix,
                        FactoryRegNo = full.FactoryRegNo,

                        ProveValidDate = validDate,
                        JoinOrCDate = full.ApplyDate,

                        EquipmentText = full.EquipmentText,
                        PrintDate = DateTime.Now
                    });
                }

                var options = new PrintOptions
                {
                    PrinterName = printerName,
                    OffsetXmm = (float)nudOffsetX.Value,
                    OffsetYmm = (float)nudOffsetY.Value,
                    PaperWidthMm = 213.5f,
                    PaperHeightMm = 280f,
                };

                AppendLog("==================================");
                AppendLog("送出列印工作：比價證明書");
                AppendLog($"印表機：{printerName}");
                AppendLog($"有效日期：{validDate}");
                AppendLog($"Offset：X={options.OffsetXmm}mm, Y={options.OffsetYmm}mm");
                AppendLog($"筆數：{items.Count}");

                var doc = BidProveBatchPrintDocumentFactory.Create(items, options);
                doc.Print();

                AppendLog("✅ 已送出列印工作（Spool）。");

                // ✅ 列印工作成功送出後，批量回寫 v_date（比價證明書有效日期）
                try
                {
                    var updated = _repo.UpdateVDateForNumbers(
                        items.Select(x => x.Number),
                        validDate // 使用者輸入的比價證明書有效日期
                    );

                    AppendLog($"✅ 已更新 v_date（比價證明書有效日期）：{updated} 筆");
                }
                catch (Exception ex2)
                {
                    // 這裡不要讓列印流程失敗，改成提示即可
                    AppendLog("⚠️ 列印已送出，但更新 v_date 失敗：" + ex2.Message);
                }

                // ✅ 你要的：按下開始列印後，清空選取並隱藏按鈕
                ClearSelectionAndHidePrint(silent: false);
            }
            catch (Exception ex)
            {
                AppendLog("❌ 列印失敗：" + ex.Message);
                MessageBox.Show(ex.Message, "列印失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // 失敗也清掉，避免「以為還能再按一次就好」造成混亂
                ClearSelectionAndHidePrint(silent: false);
            }
        }

        // =========================
        // Selection / Button States
        // =========================

        private void HidePrintUntilLoaded()
        {
            btnPrint.Visible = false;
        }

        private void ShowPrintAfterLoaded()
        {
            btnPrint.Visible = true;
        }

        private void ClearSelectionAndHidePrint(bool silent)
        {
            _selected = new List<CompanyLite>();
            HidePrintUntilLoaded();

            if (!silent)
                AppendLog("（已清空本次選取清單，若要再次列印請重新載入清單）");
        }

        private void InvalidateSelection(string reason)
        {
            // 若目前沒有載入清單，就不用一直刷 log
            if (_selected == null || _selected.Count == 0)
            {
                HidePrintUntilLoaded();
                return;
            }

            // 有載入過才需要失效
            ClearSelectionAndHidePrint(silent: true);
            AppendLog("⚠ " + reason);
        }

        private void AppendLog(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            txtLog.AppendText(line + Environment.NewLine);
        }
    }
}
