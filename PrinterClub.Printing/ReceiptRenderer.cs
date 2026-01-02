using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace PrinterClub.Printing
{
    internal sealed class ReceiptRenderer
    {
        private readonly PrintOptions _opt;

        // ✅ 文字圖快取：避免每次都重新 rasterize（效能差很多）
        private readonly Dictionary<string, Bitmap> _textCache = new();

        // ✅ 全域微調（mm）— 如果你實機/洞洞紙略偏，可只動這裡
        private const float GLOBAL_X_ADJUST_MM = 0f;
        private const float GLOBAL_Y_ADJUST_MM = 0f;

        public ReceiptRenderer(PrintOptions opt)
        {
            _opt = opt;
        }

        public void Render(Graphics g, ReceiptPrintData d)
        {
            // ✅ 避免第二次列印殘留狀態
            g.ResetTransform();

            // ✅ 最穩：Pixel + 不 transform
            g.PageUnit = GraphicsUnit.Pixel;
            g.PageScale = 1f;

            // 點陣/老機：用單色網格通常最穩（你前面也驗證過）
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

            using var font = CreateFontSafe(_opt.FontName, _opt.FontSizePt);
            var brush = Brushes.Black;

            // mm -> px
            float mmToPxX = g.DpiX / 25.4f;
            float mmToPxY = g.DpiY / 25.4f;

            float Xmm(float cm) => cm * 10f + _opt.OffsetXmm + GLOBAL_X_ADJUST_MM;
            float Ymm(float cm) => cm * 10f + _opt.OffsetYmm + GLOBAL_Y_ADJUST_MM;

            float Xpx(float cm) => Xmm(cm) * mmToPxX;
            float Ypx(float cm) => Ymm(cm) * mmToPxY;

            // ===== A) 日期 + 號（右上角）=====
            var rocY = d.PrintDate.Year - 1911;
            var m = d.PrintDate.Month;
            var day = d.PrintDate.Day;

            // (7.80,2.67) / (9.29,2.67) / (10.17,2.67) / (10.87,2.67)
            DrawTextBitmap(g, font, brush, rocY.ToString(), Xpx(7.80f), Ypx(2.67f));
            DrawTextBitmap(g, font, brush, m.ToString(), Xpx(9.29f), Ypx(2.67f));
            DrawTextBitmap(g, font, brush, day.ToString(), Xpx(10.17f), Ypx(2.67f));
            DrawTextBitmap(g, font, brush, (d.ReceiptNo ?? "").Trim(), Xpx(10.87f), Ypx(2.67f));

            // ===== B) 公司名稱 =====
            // (4.74,3.56)
            DrawTextBitmap(g, font, brush, d.CName, Xpx(4.74f), Ypx(3.56f));

            // ===== C) 會籍編號 + 所在地區別 =====
            // 你註解寫 (0.79,0.40)/(5.73,0.40) 但實際用 (4.74,4.45)/(10.67,4.45)
            // 我照你現有程式碼，因為你顯然已調過座標。
            DrawTextBitmap(g, font, brush, d.Number, Xpx(4.74f), Ypx(4.45f));
            DrawTextBitmap(g, font, brush, d.AreaClass, Xpx(10.67f), Ypx(4.45f));

            // ===== D) 起訖年月 + 會費 =====
            var (sy, sm) = YearMonthParts.TryParseRocOrIsoYm(d.StartYm);
            var (ey, em) = YearMonthParts.TryParseRocOrIsoYm(d.EndYm);

            DrawTextBitmap(g, font, brush, sy.ToString(), Xpx(4.74f), Ypx(8.10f));
            DrawTextBitmap(g, font, brush, sm.ToString(), Xpx(6.22f), Ypx(8.10f));

            DrawTextBitmap(g, font, brush, ey.ToString(), Xpx(8.49f), Ypx(8.10f));
            DrawTextBitmap(g, font, brush, em.ToString(), Xpx(9.98f), Ypx(8.10f));

            DrawTextBitmap(g, font, brush, d.Fee.ToString(), Xpx(10.67f), Ypx(8.10f));

            // ===== E) 新入會費（若 != 0 才印）=====
            if (d.NewJoinFee != 0)
            {
                DrawTextBitmap(g, font, brush, ey.ToString(), Xpx(4.74f), Ypx(8.99f));
                DrawTextBitmap(g, font, brush, d.NewJoinFee.ToString(), Xpx(10.67f), Ypx(8.99f));
            }

            // ===== F) 合計新台幣（中文大寫）=====
            var total = d.Fee + d.NewJoinFee;
            var totalZh = ChineseMoneyUpper.ToUpper(total);
            DrawTextBitmap(g, font, brush, totalZh, Xpx(5.24f), Ypx(9.78f));
        }

        private static Font CreateFontSafe(string fontName, float sizePt)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(fontName))
                    return new Font(fontName, sizePt, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch { }

            return new Font(FontFamily.GenericSansSerif, sizePt, FontStyle.Regular, GraphicsUnit.Point);
        }

        /// <summary>
        /// ✅ 橫書：把整段文字 rasterize 成 bitmap 後貼上，不用 DrawString 直接打到 driver
        /// </summary>
        private void DrawTextBitmap(Graphics g, Font font, Brush brush, string text, float xPx, float yPx)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (float.IsNaN(xPx) || float.IsNaN(yPx) || float.IsInfinity(xPx) || float.IsInfinity(yPx)) return;

            using var bmp = GetTextBitmapTight(text, font, brush, g.DpiX, g.DpiY);
            g.DrawImageUnscaled(bmp, (int)Math.Round(xPx), (int)Math.Round(yPx));
        }

        /// <summary>
        /// 產生整段文字的 bitmap（緊貼裁切），字不旋轉。
        /// </summary>
        private Bitmap GetTextBitmapTight(string text, Font font, Brush brush, float dpiX, float dpiY)
        {
            string key = $"{text}|{font.Name}|{font.SizeInPoints}|{font.Style}|{dpiX:0.##}|{dpiY:0.##}|tight";
            if (_textCache.TryGetValue(key, out var cached))
                return (Bitmap)cached.Clone();

            // 足夠大的 canvas
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

                gg.DrawString(text, font, brush, pad, pad, sf);
            }

            using var tight = CropToNonTransparent(tmp);
            _textCache[key] = (Bitmap)tight.Clone();
            return (Bitmap)tight.Clone();
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
                            byte a = row[x * 4 + 3]; // BGRA: alpha at +3
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
