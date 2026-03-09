using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Text;

namespace PrinterClub.Printing.MemberCert;

internal sealed class MemberCertRenderer : IDisposable
{
    private readonly PrintOptions _opt;
    private readonly Dictionary<string, Bitmap> _glyphCache = new();
    private bool _disposed;

    private const RotateFlipType GLYPH_ROTATE = RotateFlipType.Rotate270FlipNone;
    private const bool COLUMN_DIRECTION_UP = false;

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
        using var font10Number = CreateFontSafe(_opt.FontName, 10);
        using var font12 = CreateFontSafe(_opt.FontName, 12);
        var brush = Brushes.Black;

        float mmToPxX = g.DpiX / 25.4f;
        float mmToPxY = g.DpiY / 25.4f;

        float MmToPxX(float mm) => mm * mmToPxX;
        float MmToPxY(float mm) => mm * mmToPxY;

        float StepPxX(float stepMm) => stepMm * mmToPxX;
        float ColShiftPxY(float stepMm, float factor = 2f) => (stepMm * factor) * mmToPxY;

        // 號  x:-4  y:-4
        DrawTateText_Rightward(
            g, font10Number, brush, d.Number,
            MmToPxX(138f),
            MmToPxY(26.5f),
            StepPxX(4.6f), ColShiftPxY(4.6f, 2f), int.MaxValue
        );

        // 公司名稱  x:-8  y:-5
        DrawTateText_Rightward(
            g, font18, brush, d.CName,
            MmToPxX(25f),
            MmToPxY(63.5f),
            StepPxX(6f), ColShiftPxY(6f, 2f), 12
        );

        // 負責人姓名  x:-7  y:-4
        var who = $"{d.Chief}{SexWord(d.Sex)}";
        DrawTateText_Rightward(
            g, font18, brush, who,
            MmToPxX(76f),
            MmToPxY(115.5f),
            StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue
        );

        // 地址  y:-4
        DrawTateText_Rightward(
            g, font16, brush, NormalizeAddressForVertical(d.FAddress),
            MmToPxX(76f),
            MmToPxY(141.5f),
            StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue
        );

        // 資本（轉國語大寫） y:-4
        DrawTateText_Rightward(
            g, font16, brush, ToChineseMoneyUpper(d.Money),
            MmToPxX(107f),
            MmToPxY(165.5f),
            StepPxX(6f), ColShiftPxY(6f, 2f), 10
        );

        // 現在日期  x:-1.5  y:-6
        var p = d.PrintDate;
        var roc = p.Year - 1911;

        DrawTateText_Rightward(
            g, font18, brush, ChineseNumerals.Translate(roc),
            MmToPxX(65.5f),
            MmToPxY(239.5f),
            StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue
        );

        DrawTateText_Rightward(
            g, font18, brush, ChineseNumerals.Translate(p.Month),
            MmToPxX(115.5f),
            MmToPxY(239.5f),
            StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue
        );

        DrawTateText_Rightward(
            g, font18, brush, ChineseNumerals.Translate(p.Day),
            MmToPxX(151.5f),
            MmToPxY(239.5f),
            StepPxX(6f), ColShiftPxY(6f, 2f), int.MaxValue
        );

        // 有效期間  x:-1.5  y:-5
        var (vy, vm, vd) = DateParts.TryParseRocOrIso(d.CertValidDate);

        DrawTateText_Rightward(
            g, font12, brush, vy.ToString(),
            MmToPxX(124.5f),
            MmToPxY(253.5f),
            StepPxX(4.8f), ColShiftPxY(4.8f, 2f), int.MaxValue
        );

        DrawTateText_Rightward(
            g, font12, brush, vm.ToString(),
            MmToPxX(142.5f),
            MmToPxY(253.5f),
            StepPxX(4.8f), ColShiftPxY(4.8f, 2f), int.MaxValue
        );

        DrawTateText_Rightward(
            g, font12, brush, vd.ToString(),
            MmToPxX(156.5f),
            MmToPxY(253.5f),
            StepPxX(4.8f), ColShiftPxY(4.8f, 2f), int.MaxValue
        );
    }

    private static Font CreateFontSafe(string fontName, float sizePt)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(fontName))
                return new Font(fontName, sizePt, FontStyle.Regular, GraphicsUnit.Point);
        }
        catch
        {
        }

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
        Graphics g,
        Font font,
        Brush brush,
        string text,
        float xStartPx,
        float yStartPx,
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
                using var glyph = GetRotatedGlyphBitmapCell(ch.ToString(), font, brush, g.DpiX, g.DpiY);
                g.DrawImageUnscaled(glyph, (int)Math.Round(x), (int)Math.Round(y));
            }

            row++;
        }
    }

    private Bitmap GetRotatedGlyphBitmapCell(string s, Font font, Brush brush, float dpiX, float dpiY)
    {
        if (string.IsNullOrWhiteSpace(s))
            return CreateEmptyBitmap(dpiX, dpiY);

        int cellSize = GetGlyphCellSize(font, dpiX, dpiY);

        string key = $"{s}|{font.Name}|{font.SizeInPoints}|{font.Style}|{dpiX:0.##}|{dpiY:0.##}|rot:{GLYPH_ROTATE}|cell:{cellSize}";
        if (_glyphCache.TryGetValue(key, out var cached))
            return (Bitmap)cached.Clone();

        using var tmp = new Bitmap(cellSize, cellSize, PixelFormat.Format32bppArgb);
        tmp.SetResolution(dpiX, dpiY);

        using (var gg = Graphics.FromImage(tmp))
        {
            gg.Clear(Color.Transparent);
            gg.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

            using var sf = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoClip | StringFormatFlags.NoWrap
            };

            var rect = new RectangleF(0, 0, cellSize, cellSize);
            rect.Y -= cellSize * 0.04f;

            gg.DrawString(s, font, brush, rect, sf);
        }

        var rotated = (Bitmap)tmp.Clone();
        rotated.RotateFlip(GLYPH_ROTATE);

        var store = (Bitmap)rotated.Clone();
        rotated.Dispose();

        _glyphCache[key] = store;
        return (Bitmap)store.Clone();
    }

    private static int GetGlyphCellSize(Font font, float dpiX, float dpiY)
    {
        float pxByPt = font.SizeInPoints * Math.Max(dpiX, dpiY) / 72f;
        float pxByHeight = font.GetHeight(Math.Max(dpiX, dpiY));
        int cell = (int)Math.Ceiling(Math.Max(pxByPt, pxByHeight) * 2.2f);

        return Math.Max(cell, 64);
    }

    private static Bitmap CreateEmptyBitmap(float dpiX, float dpiY)
    {
        var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        bmp.SetResolution(dpiX, dpiY);
        return bmp;
    }

    private static string ToChineseMoneyUpper(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits))
            return raw;

        digits = digits.TrimStart('0');
        if (digits.Length == 0)
            return "零元整";

        return $"{IntegerToChineseUpper(digits)}元整";
    }

    private static string IntegerToChineseUpper(string digits)
    {
        string[] numMap = { "零", "壹", "貳", "參", "肆", "伍", "陸", "柒", "捌", "玖" };
        string[] groupUnits = { "", "萬", "億", "兆", "京" };

        var groups = SplitIntoGroupsOfFour(digits);
        var sb = new StringBuilder();
        bool pendingZero = false;

        for (int i = 0; i < groups.Count; i++)
        {
            int groupValue = int.Parse(groups[i]);
            int unitIndex = groups.Count - 1 - i;

            if (groupValue == 0)
            {
                if (sb.Length > 0)
                    pendingZero = true;
                continue;
            }

            if (sb.Length > 0 && (pendingZero || groupValue < 1000))
            {
                if (!sb.ToString().EndsWith("零", StringComparison.Ordinal))
                    sb.Append("零");
            }

            sb.Append(ConvertFourDigitsToChineseUpper(groupValue, numMap));

            if (unitIndex > 0)
                sb.Append(groupUnits[unitIndex]);

            pendingZero = false;
        }

        return sb.Length == 0 ? "零" : sb.ToString();
    }

    private static List<string> SplitIntoGroupsOfFour(string digits)
    {
        var result = new List<string>();
        for (int end = digits.Length; end > 0; end -= 4)
        {
            int start = Math.Max(0, end - 4);
            result.Insert(0, digits[start..end]);
        }
        return result;
    }

    private static string ConvertFourDigitsToChineseUpper(int value, string[] numMap)
    {
        if (value == 0) return "零";

        string[] smallUnits = { "仟", "佰", "拾", "" };
        int[] divisors = { 1000, 100, 10, 1 };

        var sb = new StringBuilder();
        bool pendingZero = false;

        for (int i = 0; i < divisors.Length; i++)
        {
            int digit = (value / divisors[i]) % 10;
            if (digit == 0)
            {
                if (sb.Length > 0)
                    pendingZero = true;
                continue;
            }

            if (pendingZero)
            {
                sb.Append("零");
                pendingZero = false;
            }

            sb.Append(numMap[digit]);
            sb.Append(smallUnits[i]);
        }

        return sb.ToString();
    }

    private static string NormalizeAddressForVertical(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        var sb = new StringBuilder(s.Length);

        foreach (var ch in s)
        {
            if (ch >= '0' && ch <= '9')
            {
                sb.Append((char)('０' + (ch - '0')));
                continue;
            }

            sb.Append(ch switch
            {
                '-' => '－',
                '(' => '（',
                ')' => '）',
                ',' => '，',
                '.' => '．',
                ':' => '：',
                ';' => '；',
                _ => ch
            });
        }

        return sb.ToString();
    }
}