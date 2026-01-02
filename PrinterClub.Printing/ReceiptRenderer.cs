using PrinterClub.Data;
using System;
using System.Drawing;

namespace PrinterClub.Printing
{
    internal sealed class ReceiptRenderer
    {
        private readonly PrintOptions _opt;

        public ReceiptRenderer(PrintOptions opt)
        {
            _opt = opt;
        }

        public void Render(Graphics g, ReceiptPrintData d)
        {
            // 舊 Java 是「格子座標」，我們已換算成 cm（含洞洞紙）
            // 你目前類庫用 mm 模式很順，就沿用：
            g.PageUnit = GraphicsUnit.Millimeter;
            g.PageScale = 1f;

            using var font = CreateFontSafe(_opt.FontName, _opt.FontSizePt);
            using var brush = Brushes.Black;

            float X(float cm) => cm * 10f + _opt.OffsetXmm;
            float Y(float cm) => cm * 10f + _opt.OffsetYmm;

            // ===== A) 日期 + 號（右上角） =====
            var rocY = d.PrintDate.Year - 1911;
            var m = d.PrintDate.Month;
            var day = d.PrintDate.Day;

            // 日期（年/月/日）＋ 號： (7.80,2.67) / (9.29,2.67) / (10.17,2.67) / (10.87,2.67)
            DrawText(g, font, brush, rocY.ToString(), X(7.80f), Y(2.67f));
            DrawText(g, font, brush, m.ToString(), X(9.29f), Y(2.67f));
            DrawText(g, font, brush, day.ToString(), X(10.17f), Y(2.67f));
            DrawText(g, font, brush, (d.ReceiptNo ?? "").Trim(), X(10.87f), Y(2.67f));

            // ===== B) 公司名稱 =====
            // cname： (4.74,3.56)
            DrawText(g, font, brush, d.CName, X(4.74f), Y(3.56f));

            // ===== C) 會籍編號 + 所在地區別 =====
            // number： (0.79,0.40)
            // area_class： (5.73,0.40)
            DrawText(g, font, brush, d.Number, X(4.74f), Y(4.45f));
            DrawText(g, font, brush, d.AreaClass, X(10.67f), Y(4.45f));

            // ===== D) 起訖年月 + 會費 =====
            // 起始年月（年/月）：(4.74,8.10) / (6.22,8.10)
            // 結束年月（年/月）：(8.49,8.10) / (9.98,8.10)
            // 會費 fee： (10.67,8.10)
            var (sy, sm) = YearMonthParts.TryParseRocOrIsoYm(d.StartYm);
            var (ey, em) = YearMonthParts.TryParseRocOrIsoYm(d.EndYm);

            DrawText(g, font, brush, sy.ToString(), X(4.74f), Y(8.10f));
            DrawText(g, font, brush, sm.ToString(), X(6.22f), Y(8.10f));

            DrawText(g, font, brush, ey.ToString(), X(8.49f), Y(8.10f));
            DrawText(g, font, brush, em.ToString(), X(9.98f), Y(8.10f));

            DrawText(g, font, brush, d.Fee.ToString(), X(10.67f), Y(8.10f));

            // ===== E) 新入會費（若 != 0 才印）=====
            // 舊 Java：若 newFee != 0 會印一行：
            // 年：(4.74,8.99)  金額：(10.67,8.99)
            // 而「年」沿用 endMonth 的年（古法怪癖），我們照做
            if (d.NewJoinFee != 0)
            {
                DrawText(g, font, brush, ey.ToString(), X(4.74f), Y(8.99f));
                DrawText(g, font, brush, d.NewJoinFee.ToString(), X(10.67f), Y(8.99f));
            }

            // ===== F) 合計新台幣（中文大寫）=====
            // (5.24,9.78)
            var total = d.Fee + d.NewJoinFee;
            var totalZh = ChineseMoneyUpper.ToUpper(total);
            DrawText(g, font, brush, totalZh, X(5.24f), Y(9.78f));
        }

        private static Font CreateFontSafe(string fontName, float sizePt)
        {
            try
            {
                return new Font(fontName, sizePt, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
                return new Font(FontFamily.GenericSansSerif, sizePt, FontStyle.Regular, GraphicsUnit.Point);
            }
        }

        private static void DrawText(Graphics g, Font f, Brush b, string text, float xMm, float yMm)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            g.DrawString(text, f, b, xMm, yMm);
        }
    }
}
