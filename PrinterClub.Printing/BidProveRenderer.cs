using PrinterClub.Data;
using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;

namespace PrinterClub.Printing;

internal sealed class BidProveRenderer
{
    private readonly PrintOptions _opt;

    public BidProveRenderer(PrintOptions opt)
    {
        _opt = opt;
    }

    public void Render(Graphics g, BidProvePrintData d)
    {
        g.ResetTransform();
        g.PageUnit = GraphicsUnit.Millimeter;
        g.PageScale = 1f;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

        using var font = CreateFontSafe(_opt.FontName, _opt.FontSizePt);
        var brush = Brushes.Black;

        float X(float cm) => cm * 10f + _opt.OffsetXmm;
        float Y(float cm) => cm * 10f + _opt.OffsetYmm;

        DrawCol(g, font, brush, d.CName, X(18.2f), Y(3.9f), "公司名稱1");
        DrawCol(g, font, brush, d.CName, X(14.3f), Y(8.3f), "公司名稱2");

        DrawCol(g, font, brush, d.Number, X(19.3f), Y(23.0f), "會籍編號");

        var (vy, vm, vd) = DateParts.TryParseRocOrIso(d.ProveValidDate);
        DrawCol(g, font, brush, ChineseNumerals.Translate(vy), X(15.8f), Y(9.3f), "比價有效年");
        DrawCol(g, font, brush, ChineseNumerals.Translate(vm), X(15.8f), Y(12.6f), "比價有效月");
        DrawCol(g, font, brush, ChineseNumerals.Translate(vd), X(15.8f), Y(15.5f), "比價有效日");

        var (jy, jm, jd) = DateParts.TryParseRocOrIso(d.JoinOrCDate);
        DrawCol(g, font, brush, ChineseNumerals.Translate(jy), X(13.5f), Y(7.8f), "入會年");
        DrawCol(g, font, brush, ChineseNumerals.Translate(jm), X(13.5f), Y(10.2f), "入會月");
        DrawCol(g, font, brush, ChineseNumerals.Translate(jd), X(13.5f), Y(12.4f), "入會日");

        DrawCol(g, font, brush, d.FAddress, X(12.8f), Y(7.0f), "工廠地址");

        var who = $"{d.Title} {d.Chief} {SexWord(d.Sex)}".Trim();
        DrawCol(g, font, brush, who, X(12.0f), Y(8.3f), "負責人");

        DrawCol(g, font, brush, d.FactoryRegPrefix, X(10.0f), Y(13.0f), "工廠登記字");
        DrawCol(g, font, brush, d.FactoryRegNo, X(10.0f), Y(15.2f), "工廠登記號");

        DrawCol(g, font, brush, d.Money, X(8.4f), Y(10.7f), "資本額");

        DrawArea(g, font, brush, d.EquipmentText, X(7.2f), Y(3.0f), "設備欄");

        var p = d.PrintDate;
        var rocYear = p.Year - 1911;
        DrawCol(g, font, brush, ChineseNumerals.Translate(rocYear), X(2.0f), Y(5.6f), "列印年");
        DrawCol(g, font, brush, ChineseNumerals.Translate(p.Month), X(2.0f), Y(9.0f), "列印月");
        DrawCol(g, font, brush, ChineseNumerals.Translate(p.Day), X(2.0f), Y(12.0f), "列印日");
    }

    private static Font CreateFontSafe(string preferName, float sizePt)
    {
        var candidates = new[]
        {
            preferName,
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

    private static string SexWord(string sex) =>
        sex switch
        {
            "F" or "f" => "女士",
            "M" or "m" => "先生",
            _ => ""
        };

    private static void DrawCol(Graphics g, Font font, Brush brush, string text, float xMm, float yMm, string fieldName)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (float.IsNaN(xMm) || float.IsNaN(yMm) || float.IsInfinity(xMm) || float.IsInfinity(yMm))
            throw new InvalidOperationException($"DrawCol 座標非法 field={fieldName}, x={xMm}, y={yMm}, text={text}");

        var step = 5.5f;
        float y = yMm;

        foreach (var ch in text)
        {
            if (char.IsControl(ch))
                continue;

            var s = ch.ToString();
            var half = ch is '-' or '_' or '.' or '(' or ')';

            try
            {
                g.DrawString(s, font, brush, xMm, y);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"DrawCol 失敗 field={fieldName}, char=[{s}], x={xMm}, y={y}, font={font.Name}, size={font.Size}",
                    ex
                );
            }

            y += half ? step * 0.5f : step;
        }
    }

    private static void DrawArea(Graphics g, Font font, Brush brush, string text, float startXmm, float startYmm, string fieldName)
    {
        if (string.IsNullOrEmpty(text)) return;

        var colShiftMm = 5.5f;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        float x = startXmm;
        foreach (var line in lines)
        {
            var normalized = NormalizeSpacesLikeJava(line);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                DrawCol(g, font, brush, normalized, x, startYmm, fieldName);
            }

            x -= colShiftMm;
        }
    }

    private static string NormalizeSpacesLikeJava(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";

        var sb = new System.Text.StringBuilder();
        int c = 0;

        foreach (var ch in s)
        {
            if (char.IsControl(ch) && ch != ' ')
                continue;

            if (ch == ' ')
            {
                c++;
                if (c == 4)
                {
                    sb.Append(' ');
                    c = 0;
                }
            }
            else
            {
                if (c != 0)
                {
                    sb.Append(' ');
                    c = 0;
                }
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }
}