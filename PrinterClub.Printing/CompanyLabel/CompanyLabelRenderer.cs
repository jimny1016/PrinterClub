using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;

namespace PrinterClub.Printing
{
    internal sealed class CompanyLabelRenderer : IDisposable
    {
        private readonly PrintOptions _opt;
        private readonly Dictionary<string, Bitmap> _glyphCache = new();
        private bool _disposed;

        // 舊 Java drawcol_r 是 -90 度，這裡沿用你前面驗證較穩的 270
        private const RotateFlipType GLYPH_ROTATE = RotateFlipType.Rotate270FlipNone;

        // 依舊 Java 推測，這份是「字往右寫，換欄往下」
        private const bool COLUMN_DIRECTION_UP = false;

        // 全域微調（mm）
        private const float GLOBAL_X_ADJUST_MM = 0f;
        private const float GLOBAL_Y_ADJUST_MM = 0f;

        public CompanyLabelRenderer(PrintOptions opt)
        {
            _opt = opt ?? throw new ArgumentNullException(nameof(opt));
        }

        public void Dispose()
        {
            if (_disposed) return;

            foreach (var bmp in _glyphCache.Values)
            {
                bmp.Dispose();
            }

            _glyphCache.Clear();
            _disposed = true;
        }

        public void Render(Graphics g, CompanyLabelPrintData d)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CompanyLabelRenderer));

            g.ResetTransform();
            g.PageUnit = GraphicsUnit.Pixel;
            g.PageScale = 1f;
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

            using var font18 = CreateFontSafe(_opt.FontName, 18);
            using var font16 = CreateFontSafe(_opt.FontName, 16);
            using var font12 = CreateFontSafe(_opt.FontName, 12);
            var brush = Brushes.Black;

            float mmToPxX = g.DpiX / 25.4f;
            float mmToPxY = g.DpiY / 25.4f;

            float Xmm(float cm) => cm * 10f + _opt.OffsetXmm + GLOBAL_X_ADJUST_MM;
            float Ymm(float cm) => cm * 10f + _opt.OffsetYmm + GLOBAL_Y_ADJUST_MM;

            float Xpx(float cm) => Xmm(cm) * mmToPxX;
            float Ypx(float cm) => Ymm(cm) * mmToPxY;

            float StepPxX(float stepMm) => stepMm * mmToPxX;
            float ColShiftPxY(float stepMm, float factor = 2f) => (stepMm * factor) * mmToPxY;

            // ===== 版面初版（依 PrintM.java 推理）
            // 這些座標是「先做出一版可印」，你到客戶現場微調 Offset 或直接微修這裡即可

            // A) 地區（橫排）
            DrawTextBitmap(g, font12, brush, d.AreaClass, Xpx(1.30f), Ypx(8.40f), "地區");

            // B) 地址（直排）
            DrawTateText_Rightward(
                g, font16, brush,
                d.AddressToPrint,
                xStartPx: Xpx(1.30f),
                yStartPx: Ypx(9.20f),
                stepPx: StepPxX(5.5f),
                colShiftPx: ColShiftPxY(5.5f, 1.8f),
                maxCharsPerCol: 18,
                fieldName: "地址"
            );

            // C) 公司名稱（直排）
            DrawTateText_Rightward(
                g, font18, brush,
                d.CName,
                xStartPx: Xpx(1.30f),
                yStartPx: Ypx(10.00f),
                stepPx: StepPxX(6.0f),
                colShiftPx: ColShiftPxY(6.0f, 1.8f),
                maxCharsPerCol: 12,
                fieldName: "公司名稱"
            );

            // D) 會籍編號（橫排）
            DrawTextBitmap(g, font12, brush, d.Number, Xpx(1.30f), Ypx(10.80f), "會籍編號");

            // E) 負責人 / 聯絡人（直排）
            DrawTateText_Rightward(
                g, font12, brush,
                d.ContactLine,
                xStartPx: Xpx(1.30f),
                yStartPx: Ypx(12.20f),
                stepPx: StepPxX(5.0f),
                colShiftPx: ColShiftPxY(5.0f, 1.7f),
                maxCharsPerCol: 20,
                fieldName: "聯絡資訊"
            );
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

        private static bool IsFinite(float v) => !(float.IsNaN(v) || float.IsInfinity(v));

        private static string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            var chars = text
                .Where(ch => !char.IsControl(ch) || ch == ' ')
                .ToArray();

            return new string(chars).Trim();
        }

        private void DrawTextBitmap(Graphics g, Font font, Brush brush, string text, float xPx, float yPx, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (!IsFinite(xPx) || !IsFinite(yPx))
                throw new InvalidOperationException($"座標非法 field={fieldName}, x={xPx}, y={yPx}");

            var sanitized = SanitizeText(text);
            if (string.IsNullOrWhiteSpace(sanitized)) return;

            using var bmp = GetTextBitmapTight(sanitized, font, brush, g.DpiX, g.DpiY, fieldName);
            g.DrawImageUnscaled(bmp, (int)Math.Round(xPx), (int)Math.Round(yPx));
        }

        private void DrawTateText_Rightward(
            Graphics g,
            Font font,
            Brush brush,
            string text,
            float xStartPx,
            float yStartPx,
            float stepPx,
            float colShiftPx,
            int maxCharsPerCol,
            string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (!IsFinite(xStartPx) || !IsFinite(yStartPx) || !IsFinite(stepPx) || !IsFinite(colShiftPx))
                throw new InvalidOperationException($"直排座標非法 field={fieldName}");

            var sanitized = SanitizeText(text);
            if (string.IsNullOrWhiteSpace(sanitized)) return;

            if (maxCharsPerCol <= 0) maxCharsPerCol = int.MaxValue;

            int col = 0;
            int row = 0;

            foreach (var ch in sanitized)
            {
                if (char.IsControl(ch))
                    continue;

                if (row >= maxCharsPerCol)
                {
                    col++;
                    row = 0;
                }

                float x = xStartPx + row * stepPx;
                float y = COLUMN_DIRECTION_UP
                    ? yStartPx - col * colShiftPx
                    : yStartPx + col * colShiftPx;

                if (IsFinite(x) && IsFinite(y))
                {
                    using var glyph = GetRotatedGlyphBitmapTight(ch.ToString(), font, brush, g.DpiX, g.DpiY, fieldName);
                    g.DrawImageUnscaled(glyph, (int)Math.Round(x), (int)Math.Round(y));
                }

                row++;
            }
        }

        private Bitmap GetTextBitmapTight(string text, Font font, Brush brush, float dpiX, float dpiY, string fieldName)
        {
            string key = $"{text}|{font.Name}|{font.SizeInPoints}|{font.Style}|{dpiX:0.##}|{dpiY:0.##}|text-tight";
            if (_glyphCache.TryGetValue(key, out var cached))
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
            _glyphCache[key] = store;

            return (Bitmap)store.Clone();
        }

        private Bitmap GetRotatedGlyphBitmapTight(string s, Font font, Brush brush, float dpiX, float dpiY, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(s))
                return CreateEmptyBitmap(dpiX, dpiY);

            string key = $"{s}|{font.Name}|{font.SizeInPoints}|{font.Style}|{dpiX:0.##}|{dpiY:0.##}|rot:{GLYPH_ROTATE}";
            if (_glyphCache.TryGetValue(key, out var cached))
                return (Bitmap)cached.Clone();

            int canvas = (int)Math.Ceiling(font.SizeInPoints * Math.Max(dpiX, dpiY) / 72f) * 4;
            canvas = Math.Max(canvas, 96);

            using var tmp = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);
            tmp.SetResolution(dpiX, dpiY);

            using (var gg = Graphics.FromImage(tmp))
            {
                gg.Clear(Color.Transparent);
                gg.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

                using var sf = new StringFormat(StringFormat.GenericTypographic)
                {
                    FormatFlags = StringFormatFlags.NoClip | StringFormatFlags.NoWrap
                };

                var center = new PointF(canvas / 2f, canvas / 2f);
                var size = gg.MeasureString(s, font, canvas, sf);

                float x = center.X - size.Width / 2f;
                float y = center.Y - size.Height / 2f;

                try
                {
                    gg.DrawString(s, font, brush, x, y, sf);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Glyph DrawString 失敗 field={fieldName}, char=[{s}], font={font.Name}, size={font.Size}, dpiX={dpiX}, dpiY={dpiY}",
                        ex
                    );
                }
            }

            using var tight = CropToNonTransparent(tmp);
            var rotated = (Bitmap)tight.Clone();
            rotated.RotateFlip(GLYPH_ROTATE);

            using var tight2 = CropToNonTransparent(rotated);
            rotated.Dispose();

            var store = (Bitmap)tight2.Clone();
            _glyphCache[key] = store;

            return (Bitmap)store.Clone();
        }

        private static Bitmap CreateEmptyBitmap(float dpiX, float dpiY)
        {
            var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
            bmp.SetResolution(dpiX, dpiY);
            return bmp;
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