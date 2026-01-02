using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace PrinterClub.Printing;

internal sealed class MemberCertRenderer
{
    private readonly PrintOptions _opt;

    // 字元圖快取：避免每個字都重做 Bitmap（效能差很多）
    private readonly Dictionary<string, Bitmap> _glyphCache = new();

    // 你已確認這個方向正確
    private const RotateFlipType GLYPH_ROTATE = RotateFlipType.Rotate270FlipNone;

    // ✅ 你說「換行方向相反」：這裡反轉
    // false = 換欄往下（y 增加）; true = 換欄往上（y 減少）
    private const bool COLUMN_DIRECTION_UP = false;

    // ✅ 全域微調（mm）
    // 你覺得整體往下偏一行：先用「緊貼裁切」通常就會回正；
    // 如果仍有小偏差，可在這裡做最後校正（例如 -0.5f 或 -1.0f）
    private const float GLOBAL_Y_ADJUST_MM = -2.5f;
    private const float GLOBAL_X_ADJUST_MM = 0f;

    public MemberCertRenderer(PrintOptions opt)
    {
        _opt = opt;
    }

    public void Render(Graphics g, MemberCertPrintData d)
    {
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

        // ✅ 你要的（直立看時）書寫方向：
        // 字往右寫 => row 推進用 X
        float StepPxX(float stepMm) => stepMm * mmToPxX;

        // 換欄用 Y（往上/下）
        float ColShiftPxY(float stepMm, float factor = 2f) => (stepMm * factor) * mmToPxY;

        // A) 會籍編號
        DrawTateText_Rightward(
            g, font16, brush, d.Number,
            xStartPx: Xpx(16.0f), yStartPx: Ypx(5.3f),
            stepPx: StepPxX(6f),
            colShiftPx: ColShiftPxY(6f, 2f),
            maxCharsPerCol: int.MaxValue
        );

        // B) 公司名稱（12 字換欄）
        DrawTateText_Rightward(
            g, font18, brush, d.CName,
            Xpx(4.8f), Ypx(9.5f),
            StepPxX(6f), ColShiftPxY(6f, 2f),
            maxCharsPerCol: 12
        );

        // C) 負責人 + 稱謂
        var who = $"{d.Chief}{SexWord(d.Sex)}";
        DrawTateText_Rightward(
            g, font18, brush, who,
            Xpx(9.8f), Ypx(14.4f),
            StepPxX(6f), ColShiftPxY(6f, 2f),
            maxCharsPerCol: int.MaxValue
        );

        // D) 工廠地址
        DrawTateText_Rightward(
            g, font16, brush, d.FAddress,
            Xpx(9.8f), Ypx(16.8f),
            StepPxX(6f), ColShiftPxY(6f, 2f),
            maxCharsPerCol: int.MaxValue
        );

        // E) 資本額（10 字換欄）
        DrawTateText_Rightward(
            g, font16, brush, d.Money,
            Xpx(12.6f), Ypx(19.3f),
            StepPxX(6f), ColShiftPxY(6f, 2f),
            maxCharsPerCol: 10
        );

        // F) 列印日（中文數字）
        var p = d.PrintDate;
        var roc = p.Year - 1911;

        DrawTateText_Rightward(g, font18, brush, ChineseNumerals.Translate(roc), Xpx(9.5f), Ypx(26.6f), StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue);
        DrawTateText_Rightward(g, font18, brush, ChineseNumerals.Translate(p.Month), Xpx(13.0f), Ypx(26.6f), StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue);
        DrawTateText_Rightward(g, font18, brush, ChineseNumerals.Translate(p.Day), Xpx(16.6f), Ypx(26.6f), StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue);

        // G) 會員證書有效日期（步距較小）
        var (vy, vm, vd) = DateParts.TryParseRocOrIso(d.CertValidDate);

        DrawTateText_Rightward(g, font12, brush, vy.ToString(), Xpx(14.6f), Ypx(27.8f), StepPxX(4.8f), ColShiftPxY(4.8f, 2f), int.MaxValue);
        DrawTateText_Rightward(g, font12, brush, vm.ToString(), Xpx(16.2f), Ypx(27.8f), StepPxX(4.8f), ColShiftPxY(4.8f, 2f), int.MaxValue);
        DrawTateText_Rightward(g, font12, brush, vd.ToString(), Xpx(17.6f), Ypx(27.8f), StepPxX(4.8f), ColShiftPxY(4.8f, 2f), int.MaxValue);
    }

    // ===== helpers =====

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

    /// <summary>
    /// ✅ 直立看時：
    /// - 字往右寫（x += step）
    /// - 換欄：由 COLUMN_DIRECTION_UP 決定（這次你說相反 -> 已改成 false）
    /// - 每字 bitmap 旋轉貼上（不使用印表機 transform）
    /// </summary>
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
            if (row >= maxCharsPerCol)
            {
                col++;
                row = 0;
            }

            float x = xStartPx + row * stepPx;

            // ✅ 這裡就是「換行方向」：你說反了，所以現在改成往下（COLUMN_DIRECTION_UP=false）
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

    /// <summary>
    /// 產生「單一字元」bitmap：
    /// - 先畫到大 canvas
    /// - 用透明像素掃描做「緊貼裁切」（解決你說的整體偏移問題）
    /// - 再旋轉
    /// - 旋轉後再裁一次（避免旋轉造成新的 padding）
    /// </summary>
    private Bitmap GetRotatedGlyphBitmapTight(string s, Font font, Brush brush, float dpiX, float dpiY)
    {
        string key = $"{s}|{font.Name}|{font.SizeInPoints}|{font.Style}|{dpiX:0.##}|{dpiY:0.##}|rot:{GLYPH_ROTATE}|tight";
        if (_glyphCache.TryGetValue(key, out var cached))
            return (Bitmap)cached.Clone();

        // 大一點，避免字被裁掉
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

            // 畫在中間，避免外框溢出
            var center = new PointF(canvas / 2f, canvas / 2f);
            var size = gg.MeasureString(s, font, center, sf);
            float x = center.X - size.Width / 2f;
            float y = center.Y - size.Height / 2f;

            gg.DrawString(s, font, brush, x, y, sf);
        }

        using var tight = CropToNonTransparent(tmp);
        var rotated = (Bitmap)tight.Clone();
        rotated.RotateFlip(GLYPH_ROTATE);

        using var tight2 = CropToNonTransparent(rotated);

        _glyphCache[key] = (Bitmap)tight2.Clone();
        return (Bitmap)tight2.Clone();
    }

    /// <summary>
    /// 用 alpha 掃描裁切到最小 bounding box（緊貼字形）
    /// </summary>
    private static Bitmap CropToNonTransparent(Bitmap src)
    {
        Rectangle bounds = FindNonTransparentBounds(src);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            // 真的沒有內容：回傳 1x1 透明
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
        // 直接用 LockBits 掃 alpha（比 GetPixel 快很多）
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
                        // BGRA，alpha 在 +3
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
