using PrinterClub.Data;
using System;
using System.Drawing;
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
        g.PageUnit = GraphicsUnit.Millimeter;
        g.PageScale = 1f;

        using var font = CreateFontSafe(_opt.FontName, _opt.FontSizePt);
        using var brush = Brushes.Black;

        float X(float cm) => cm * 10f + _opt.OffsetXmm;
        float Y(float cm) => cm * 10f + _opt.OffsetYmm;

        DrawCol(g, font, brush, d.CName, X(18.2f), Y(3.9f));
        DrawCol(g, font, brush, d.CName, X(14.3f), Y(8.3f));

        DrawCol(g, font, brush, d.Number, X(19.3f), Y(23.0f));

        var (vy, vm, vd) = DateParts.TryParseRocOrIso(d.ProveValidDate);
        DrawCol(g, font, brush, ChineseNumerals.Translate(vy), X(15.8f), Y(9.3f));
        DrawCol(g, font, brush, ChineseNumerals.Translate(vm), X(15.8f), Y(12.6f));
        DrawCol(g, font, brush, ChineseNumerals.Translate(vd), X(15.8f), Y(15.5f));

        var (jy, jm, jd) = DateParts.TryParseRocOrIso(d.JoinOrCDate);
        DrawCol(g, font, brush, ChineseNumerals.Translate(jy), X(13.5f), Y(7.8f));
        DrawCol(g, font, brush, ChineseNumerals.Translate(jm), X(13.5f), Y(10.2f));
        DrawCol(g, font, brush, ChineseNumerals.Translate(jd), X(13.5f), Y(12.4f));

        DrawCol(g, font, brush, d.FAddress, X(12.8f), Y(7.0f));

        var who = $"{d.Title} {d.Chief} {SexWord(d.Sex)}".Trim();
        DrawCol(g, font, brush, who, X(12.0f), Y(8.3f));

        DrawCol(g, font, brush, d.FactoryRegPrefix, X(10.0f), Y(13.0f));
        DrawCol(g, font, brush, d.FactoryRegNo, X(10.0f), Y(15.2f));

        DrawCol(g, font, brush, d.Money, X(8.4f), Y(10.7f));

        DrawArea(g, font, brush, d.EquipmentText, X(7.2f), Y(3.0f));

        var p = d.PrintDate;
        var rocYear = p.Year - 1911;
        DrawCol(g, font, brush, ChineseNumerals.Translate(rocYear), X(2.0f), Y(5.6f));
        DrawCol(g, font, brush, ChineseNumerals.Translate(p.Month), X(2.0f), Y(9.0f));
        DrawCol(g, font, brush, ChineseNumerals.Translate(p.Day), X(2.0f), Y(12.0f));
    }

    private static Font CreateFontSafe(string preferName, float sizePt)
    {
        // 你可以依實機調整候選字型排序（點陣常見：新細明體/細明體較穩）
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
                // 檢查字型是否存在（避免直接 new Font 爆炸）
                if (!FontFamily.Families.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                return new Font(name, sizePt, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
                // 試下一個
            }
        }

        // 最後保底
        return new Font(FontFamily.GenericSansSerif, sizePt, FontStyle.Regular, GraphicsUnit.Point);
    }

    private static string SexWord(string sex) =>
        sex switch
        {
            "F" or "f" => "女士",
            "M" or "m" => "先生",
            _ => ""
        };

    private static void DrawCol(Graphics g, Font font, Brush brush, string text, float xMm, float yMm)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 基本防呆：NaN / Infinity 會直接炸 GDI+
        if (float.IsNaN(xMm) || float.IsNaN(yMm) || float.IsInfinity(xMm) || float.IsInfinity(yMm))
            throw new InvalidOperationException($"DrawCol 座標非法 x={xMm}, y={yMm}, text={text}");

        var step = 5.5f;

        float y = yMm;
        foreach (var ch in text)
        {
            var half = ch is '-' or '_' or '.' or '(' or ')';
            g.DrawString(ch.ToString(), font, brush, xMm, y);
            y += half ? step * 0.5f : step;
        }
    }

    private static void DrawArea(Graphics g, Font font, Brush brush, string text, float startXmm, float startYmm)
    {
        if (string.IsNullOrEmpty(text)) return;

        var colShiftMm = 5.5f;
        var lines = text.Replace("\r\n", "\n").Split('\n');

        float x = startXmm;
        foreach (var line in lines)
        {
            var normalized = NormalizeSpacesLikeJava(line);
            DrawCol(g, font, brush, normalized, x, startYmm);
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
