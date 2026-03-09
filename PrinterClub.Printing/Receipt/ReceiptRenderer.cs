using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;

namespace PrinterClub.Printing.Receipt
{
    internal sealed class ReceiptRenderer : IDisposable
    {
        private readonly PrintOptions _opt;
        private readonly Dictionary<string, Bitmap> _textCache = new();
        private bool _disposed;

        private const float GLOBAL_X_ADJUST_MM = 0f;
        private const float GLOBAL_Y_ADJUST_MM = 0f;

        public ReceiptRenderer(PrintOptions opt)
        {
            _opt = opt ?? throw new ArgumentNullException(nameof(opt));
        }

        public void Dispose()
        {
            if (_disposed) return;

            foreach (var bmp in _textCache.Values)
            {
                bmp.Dispose();
            }

            _textCache.Clear();
            _disposed = true;
        }

        public void Render(Graphics g, ReceiptPrintData d)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ReceiptRenderer));

            g.ResetTransform();
            g.PageUnit = GraphicsUnit.Pixel;
            g.PageScale = 1f;
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

            using var font = CreateFontSafe(_opt.FontName, _opt.FontSizePt);
            var brush = Brushes.Black;

            float mmToPxX = g.DpiX / 25.4f;
            float mmToPxY = g.DpiY / 25.4f;

            float Xmm(float cm) => cm * 10f + _opt.OffsetXmm + GLOBAL_X_ADJUST_MM;
            float Ymm(float cm) => cm * 10f + _opt.OffsetYmm + GLOBAL_Y_ADJUST_MM;

            float Xpx(float cm, float adjustMm = 0f) => (Xmm(cm) + adjustMm) * mmToPxX;
            float Ypx(float cm, float adjustMm = 0f) => (Ymm(cm) + adjustMm) * mmToPxY;

            var rocY = d.PrintDate.Year - 1911;
            var m = d.PrintDate.Month;
            var day = d.PrintDate.Day;

            // 最上方年月日：x + 10mm
            DrawTextBitmap(g, font, brush, rocY.ToString(), Xpx(7.80f, 12f), Ypx(2.67f), "列印年");
            DrawTextBitmap(g, font, brush, m.ToString(), Xpx(9.29f, 12f), Ypx(2.67f), "列印月");
            DrawTextBitmap(g, font, brush, day.ToString(), Xpx(10.17f, 12f), Ypx(2.67f), "列印日");

            // 號：x + 35mm
            DrawTextBitmap(g, font, brush, (d.ReceiptNo ?? "").Trim(), Xpx(10.87f, 41f), Ypx(2.67f), "收據號碼");

            // 場名 / 公司名稱：x + 6mm
            DrawTextBitmap(g, font, brush, d.CName, Xpx(4.74f, 6f), Ypx(3.56f), "公司名稱");

            // 編號：x + 6mm
            DrawTextBitmap(g, font, brush, d.Number, Xpx(4.74f, 6f), Ypx(4.45f), "會籍編號");

            // 地區別：x + 10mm
            DrawTextBitmap(g, font, brush, d.AreaClass, Xpx(10.67f, 10f), Ypx(4.45f), "地區");

            var (sy, sm) = YearMonthParts.TryParseRocOrIsoYm(d.StartYm);
            var (ey, em) = YearMonthParts.TryParseRocOrIsoYm(d.EndYm);

            // 起始 / 結束年月：都 x + 10mm
            DrawTextBitmap(g, font, brush, sy.ToString(), Xpx(4.74f, 10f), Ypx(8.10f), "起始年");
            DrawTextBitmap(g, font, brush, sm.ToString(), Xpx(6.22f, 10f), Ypx(8.10f), "起始月");
            DrawTextBitmap(g, font, brush, ey.ToString(), Xpx(8.49f, 10f), Ypx(8.10f), "結束年");
            DrawTextBitmap(g, font, brush, em.ToString(), Xpx(9.98f, 10f), Ypx(8.10f), "結束月");

            // 月費：x + 45mm
            DrawTextBitmap(g, font, brush, d.Fee.ToString(), Xpx(10.67f, 45f), Ypx(8.10f), "常年會費");

            if (d.NewJoinFee != 0)
            {
                // 下一行的年：x + 10mm
                DrawTextBitmap(g, font, brush, ey.ToString(), Xpx(4.74f, 10f), Ypx(8.99f), "新入會費年");

                // 新入會費：x + 45mm
                DrawTextBitmap(g, font, brush, d.NewJoinFee.ToString(), Xpx(10.67f, 45f), Ypx(8.99f), "新入會費");
            }

            var total = d.Fee + d.NewJoinFee;
            var totalZh = ChineseMoneyUpper.ToUpper(total);

            // 總計：x + 10mm
            DrawTextBitmap(g, font, brush, totalZh, Xpx(5.24f, 10f), Ypx(9.78f), "中文大寫金額");
        }

        private static Font CreateFontSafe(string fontName, float sizePt)
        {
            var candidates = new[]
            {
                fontName,
                "標楷體",
                "新細明體",
                "細明體",
                "Microsoft JhengHei",
                "PMingLiU",
                "MingLiU"
            }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

            foreach (var name in candidates)
            {
                try
                {
                    if (!FontFamily.Families.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    return new Font(name, sizePt, FontStyle.Regular, GraphicsUnit.Point);
                }
                catch
                {
                }
            }

            return new Font(FontFamily.GenericSansSerif, sizePt, FontStyle.Regular, GraphicsUnit.Point);
        }

        private void DrawTextBitmap(Graphics g, Font font, Brush brush, string text, float xPx, float yPx, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (float.IsNaN(xPx) || float.IsNaN(yPx) || float.IsInfinity(xPx) || float.IsInfinity(yPx))
                throw new InvalidOperationException($"座標非法 field={fieldName}, x={xPx}, y={yPx}");

            var sanitized = SanitizeText(text);
            if (string.IsNullOrWhiteSpace(sanitized)) return;

            using var bmp = GetTextBitmapTight(sanitized, font, brush, g.DpiX, g.DpiY, fieldName);
            g.DrawImageUnscaled(bmp, (int)Math.Round(xPx), (int)Math.Round(yPx));
        }

        private Bitmap GetTextBitmapTight(string text, Font font, Brush brush, float dpiX, float dpiY, string fieldName)
        {
            string key = $"{text}|{font.Name}|{font.SizeInPoints}|{font.Style}|{dpiX:0.##}|{dpiY:0.##}|tight";

            if (_textCache.TryGetValue(key, out var cached))
                return (Bitmap)cached.Clone();

            int pad = 10;
            int estW = Math.Max(96, (int)Math.Ceiling(text.Length * font.SizeInPoints * dpiX / 72f) + pad * 2);
            int estH = Math.Max(64, (int)Math.Ceiling(font.SizeInPoints * dpiY / 72f) * 3 + pad * 2);

            using var tmp = new Bitmap(estW, estH, PixelFormat.Format32bppArgb);
            tmp.SetResolution(dpiX, dpiY);

            using (var gg = Graphics.FromImage(tmp))
            {
                gg.Clear(Color.Transparent);
                gg.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

                using var sf = new StringFormat(StringFormat.GenericTypographic)
                {
                    FormatFlags = StringFormatFlags.NoClip | StringFormatFlags.NoWrap
                };

                try
                {
                    gg.DrawString(text, font, brush, pad, pad, sf);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"DrawString 失敗 field={fieldName}, text=[{text}], font={font.Name}, size={font.Size}, dpiX={dpiX}, dpiY={dpiY}",
                        ex
                    );
                }
            }

            using var tight = CropToNonTransparent(tmp);
            var store = (Bitmap)tight.Clone();
            _textCache[key] = store;

            return (Bitmap)store.Clone();
        }

        private static string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            var chars = text
                .Where(ch => !char.IsControl(ch) || ch == ' ')
                .ToArray();

            return new string(chars).Trim();
        }

        private static Bitmap CropToNonTransparent(Bitmap src)
        {
            Rectangle bounds = FindNonTransparentBounds(src);

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                var empty = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
                empty.SetResolution(src.HorizontalResolution, src.VerticalResolution);
                return empty;
            }

            var dst = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            dst.SetResolution(src.HorizontalResolution, src.VerticalResolution);

            using (var g = Graphics.FromImage(dst))
            {
                g.Clear(Color.Transparent);
                g.DrawImage(src, new Rectangle(0, 0, dst.Width, dst.Height), bounds, GraphicsUnit.Pixel);
            }

            return dst;
        }

        private static Rectangle FindNonTransparentBounds(Bitmap bmp)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* scan0 = (byte*)data.Scan0;
                    int stride = data.Stride;

                    int minX = bmp.Width, minY = bmp.Height, maxX = -1, maxY = -1;

                    for (int y = 0; y < bmp.Height; y++)
                    {
                        byte* row = scan0 + y * stride;

                        for (int x = 0; x < bmp.Width; x++)
                        {
                            byte a = row[x * 4 + 3];
                            if (a != 0)
                            {
                                if (x < minX) minX = x;
                                if (y < minY) minY = y;
                                if (x > maxX) maxX = x;
                                if (y > maxY) maxY = y;
                            }
                        }
                    }

                    if (maxX < minX || maxY < minY)
                        return Rectangle.Empty;

                    return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }
    }
}