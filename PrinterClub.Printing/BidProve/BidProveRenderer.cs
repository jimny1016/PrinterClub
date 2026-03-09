using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Text;

namespace PrinterClub.Printing.BidProve;

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

        // 公司名稱1
        DrawCol(g, font, brush, d.CName, 179.5f, 32f, "公司名稱1");

        // 公司名稱2
        DrawCol(g, font, brush, d.CName, 139.5f, 75f, "公司名稱2");

        // 會籍編號
        DrawCol(g, font, brush, d.Number, 190.5f, 224f, "會籍編號");

        // 比價有效日期
        var (vy, vm, vd) = DateParts.TryParseRocOrIso(d.ProveValidDate);
        DrawCol(g, font, brush, ChineseNumerals.Translate(vy), 155.5f, 95f, "比價有效年");
        DrawCol(g, font, brush, ChineseNumerals.Translate(vm), 155.5f, 128f, "比價有效月");
        DrawCol(g, font, brush, ChineseNumerals.Translate(vd), 155.5f, 157f, "比價有效日");

        // 入會日期
        var (jy, jm, jd) = DateParts.TryParseRocOrIso(d.JoinOrCDate);
        DrawCol(g, font, brush, ChineseNumerals.Translate(jy), 131.5f, 76f, "入會年");
        DrawCol(g, font, brush, ChineseNumerals.Translate(jm), 131.5f, 105f, "入會月");
        DrawCol(g, font, brush, ChineseNumerals.Translate(jd), 131.5f, 127f, "入會日");

        // 工廠地址
        DrawCol(g, font, brush, d.FAddress, 123.5f, 60f, "工廠地址");

        // 負責人
        var who = $"{d.Title} {d.Chief} {SexWord(d.Sex)}".Trim();
        DrawCol(g, font, brush, who, 117.5f, 83f, "負責人");

        // 工廠登記字 / 號
        DrawCol(g, font, brush, d.FactoryRegPrefix, 95.5f, 133f, "工廠登記字");
        DrawCol(g, font, brush, d.FactoryRegNo, 95.5f, 155f, "工廠登記號");

        // 資本額（轉國語大寫）
        DrawCol(g, font, brush, ToChineseMoneyUpper(d.Money), 80.5f, 100f, "資本額");

        // 設備欄
        DrawArea(g, font, brush, d.EquipmentText, 45.5f, 30f, "設備欄");

        // 列印日期
        var p = d.PrintDate;
        var rocYear = p.Year - 1911;
        DrawCol(g, font, brush, ChineseNumerals.Translate(rocYear), 13.5f, 45f, "列印年");
        DrawCol(g, font, brush, ChineseNumerals.Translate(p.Month), 13.5f, 84f, "列印月");
        DrawCol(g, font, brush, ChineseNumerals.Translate(p.Day), 13.5f, 114f, "列印日");
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

        const float step = 5.5f;
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

        const float colShiftMm = 5.5f;
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

        var sb = new StringBuilder();
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
}