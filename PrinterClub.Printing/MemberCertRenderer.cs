using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace PrinterClub.Printing;

internal sealed class MemberCertRenderer : IDisposable
{
    private readonly PrintOptions _opt;
    private readonly Dictionary<string, Bitmap> _glyphCache = new();
    private bool _disposed;

    private const RotateFlipType GLYPH_ROTATE = RotateFlipType.Rotate270FlipNone;
    private const bool COLUMN_DIRECTION_UP = false;
    private const float GLOBAL_Y_ADJUST_MM = -2.5f;
    private const float GLOBAL_X_ADJUST_MM = 0f;

    public MemberCertRenderer(PrintOptions opt)
    {
        _opt = opt;
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

    public void Render(Graphics g, MemberCertPrintData d)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MemberCertRenderer));

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

        DrawTateText_Rightward(g, font16, brush, d.Number, Xpx(16.0f), Ypx(5.3f), StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue);
        DrawTateText_Rightward(g, font18, brush, d.CName, Xpx(4.8f), Ypx(9.5f), StepPxX(6f), ColShiftPxY(6f, 2f), 12);

        var who = $"{d.Chief}{SexWord(d.Sex)}";
        DrawTateText_Rightward(g, font18, brush, who, Xpx(9.8f), Ypx(14.4f), StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue);

        DrawTateText_Rightward(g, font16, brush, d.FAddress, Xpx(9.8f), Ypx(16.8f), StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue);
        DrawTateText_Rightward(g, font16, brush, d.Money, Xpx(12.6f), Ypx(19.3f), StepPxX(6f), ColShiftPxY(6f, 2f), 10);

        var p = d.PrintDate;
        var roc = p.Year - 1911;

        DrawTateText_Rightward(g, font18, brush, ChineseNumerals.Translate(roc), Xpx(9.5f), Ypx(26.6f), StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue);
        DrawTateText_Rightward(g, font18, brush, ChineseNumerals.Translate(p.Month), Xpx(13.0f), Ypx(26.6f), StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue);
        DrawTateText_Rightward(g, font18, brush, ChineseNumerals.Translate(p.Day), Xpx(16.6f), Ypx(26.6f), StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue);

        var (vy, vm, vd) = DateParts.TryParseRocOrIso(d.CertValidDate);
        DrawTateText_Rightward(g, font12, brush, vy.ToString(), Xpx(14.6f), Ypx(27.8f), StepPxX(4.8f), ColShiftPxY(4.8f, 2f), int.MaxValue);
        DrawTateText_Rightward(g, font12, brush, vm.ToString(), Xpx(16.2f), Ypx(27.8f), StepPxX(4.8f), ColShiftPxY(4.8f, 2f), int.MaxValue);
        DrawTateText_Rightward(g, font12, brush, vd.ToString(), Xpx(17.6f), Ypx(27.8f), StepPxX(4.8f), ColShiftPxY(4.8f, 2f), int.MaxValue);
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

    private static string SexWord(string sex) =>
        sex switch
        {
            "F" or "f" => "女士",
            "M" or "m" => "先生",
            _ => ""
        };

    private static bool IsFinite(float v) => !(float.IsNaN(v) || float.IsInfinity(v));

    private void DrawTateText_Rightward(
        Graphics g, Font font, Brush brush,
        string text,
        float xStartPx, float yStartPx,
        float stepPx,
        float colShiftPx,
        int maxCharsPerCol)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!IsFinite(xStartPx) || !IsFinite(yStartPx) || !IsFinite(stepPx) || !IsFinite(colShiftPx)) return;
        if (maxCharsPerCol <= 0) maxCharsPerCol = int.MaxValue;

        int col = 0;
        int row = 0;

        foreach (var ch in text)
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
                using var glyph = GetRotatedGlyphBitmapTight(ch.ToString(), font, brush, g.DpiX, g.DpiY);
                g.DrawImageUnscaled(glyph, (int)Math.Round(x), (int)Math.Round(y));
            }

            row++;
        }
    }

    private Bitmap GetRotatedGlyphBitmapTight(string s, Font font, Brush brush, float dpiX, float dpiY)
    {
        if (string.IsNullOrWhiteSpace(s))
            return CreateEmptyBitmap(dpiX, dpiY);

        string key = $"{s}|{font.Name}|{font.SizeInPoints}|{font.Style}|{dpiX:0.##}|{dpiY:0.##}|rot:{GLYPH_ROTATE}|tight";
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

            gg.DrawString(s, font, brush, x, y, sf);
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