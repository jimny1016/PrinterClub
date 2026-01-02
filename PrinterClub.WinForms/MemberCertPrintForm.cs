using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using PrinterClub.Data;
using PrinterClub.Printing;

namespace PrinterClub.WinForms
{
    public class MemberCertPrintForm : Form
    {
        private readonly CompanyRepository _repo;

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

        private List<CompanyLite> _selected = new();

        public MemberCertPrintForm(CompanyRepository repo, string? defaultFrom = null, string? defaultTo = null)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            Text = "列印 - 會員證書";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 520);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            BuildUi();

            txtFrom.Text = (defaultFrom ?? "").Trim();
            txtTo.Text = (defaultTo ?? "").Trim();

            LoadPrinters();
            HidePrintUntilLoaded();

            AppendLog("請選擇印表機、輸入會籍編號範圍與會員證書有效日期，按「載入清單」。");
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            root.Controls.Add(left, 0, 0);

            int y = 10;
            Label L(string t) => new Label { Text = t, AutoSize = true, Left = 10, Top = y + 6 };
            TextBox T(int w = 200) => new TextBox { Left = 140, Top = y, Width = w };

            // 印表機
            left.Controls.Add(L("印表機"));
            cmbPrinters = new ComboBox { Left = 140, Top = y, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
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
            y += 34;

            // 有效日期
            left.Controls.Add(L("證書有效日期"));
            txtValidDate = T(160);
            txtValidDate.PlaceholderText = "例：107.12.31";
            left.Controls.Add(txtValidDate);
            y += 40;

            // Offset
            left.Controls.Add(L("Offset X (mm)"));
            nudOffsetX = new NumericUpDown { Left = 140, Top = y, Width = 120, DecimalPlaces = 1, Minimum = -50, Maximum = 50 };
            left.Controls.Add(nudOffsetX);
            y += 34;

            left.Controls.Add(L("Offset Y (mm)"));
            nudOffsetY = new NumericUpDown { Left = 140, Top = y, Width = 120, DecimalPlaces = 1, Minimum = -50, Maximum = 50 };
            left.Controls.Add(nudOffsetY);
            y += 44;

            btnLoadList = new Button { Text = "載入清單", Left = 10, Top = y, Width = 110 };
            btnPrint = new Button { Text = "開始列印", Left = 130, Top = y, Width = 110 };
            btnClose = new Button { Text = "關閉", Left = 250, Top = y, Width = 90 };

            btnLoadList.Click += (_, __) => LoadSelection();
            btnPrint.Click += (_, __) => DoPrint();
            btnClose.Click += (_, __) => Close();

            left.Controls.Add(btnLoadList);
            left.Controls.Add(btnPrint);
            left.Controls.Add(btnClose);

            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            root.Controls.Add(right, 1, 0);

            right.Controls.Add(new Label { Text = "LOG / 本次列印清單", AutoSize = true });

            txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Top = 24
            };
            right.Controls.Add(txtLog);
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
            _selected.Clear();
            HidePrintUntilLoaded();

            var from = txtFrom.Text.Trim();
            var to = txtTo.Text.Trim();

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                MessageBox.Show("請輸入會籍編號範圍");
                return;
            }

            _selected = _repo.SearchByNumberRange(from, to, 5000);

            AppendLog($"載入範圍：{from} ~ {to}");
            AppendLog($"選取筆數：{_selected.Count}");

            foreach (var c in _selected)
                AppendLog($"- {c.Number} {c.CName}");

            if (_selected.Count > 0)
                ShowPrintAfterLoaded();
        }

        private void DoPrint()
        {
            if (_selected.Count == 0) return;

            var validDate = (txtValidDate.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(validDate))
            {
                MessageBox.Show("請輸入比價證明書有效日期（例：107.12.31）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var items = new List<MemberCertPrintData>();
            foreach (var c in _selected)
            {
                var full = _repo.GetByNumber(c.Number) ?? c;

                items.Add(new MemberCertPrintData
                {
                    Number = full.Number,
                    CName = full.CName,
                    Chief = full.Chief,
                    Sex = full.Sex,
                    FAddress = full.FAddress,
                    Money = full.Money,
                    CertValidDate = validDate,
                    PrintDate = DateTime.Now
                });
            }

            var opt = new PrintOptions
            {
                PrinterName = cmbPrinters.SelectedItem!.ToString()!,
                OffsetXmm = (float)nudOffsetX.Value,
                OffsetYmm = (float)nudOffsetY.Value,
                PaperWidthMm = 210,
                PaperHeightMm = 297
            };

            using var doc = MemberCertBatchPrintDocumentFactory.Create(items, opt);
            doc.Print();

            AppendLog("✅ 已送出列印工作");

            // ✅ 列印工作成功送出後，批量回寫 v_date2（會員證書有效日期）
            try
            {
                var updated = _repo.UpdateVDate2ForNumbers(
                    items.Select(x => x.Number),
                    validDate // 使用者輸入的會員證書有效日期
                );

                AppendLog($"✅ 已更新 v_date2（會員證書有效日期）：{updated} 筆");
            }
            catch (Exception ex2)
            {
                AppendLog("⚠️ 列印已送出，但更新 v_date2 失敗：" + ex2.Message);
            }

            _selected.Clear();
            HidePrintUntilLoaded();
        }

        private void HidePrintUntilLoaded() => btnPrint.Visible = false;
        private void ShowPrintAfterLoaded() => btnPrint.Visible = true;

        private void AppendLog(string msg)
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }
    }
}
