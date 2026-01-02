using PrinterClub.Data;
using System.Drawing;

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
        // 用 mm 座標
        g.PageUnit = GraphicsUnit.Millimeter;
        g.PageScale = 1f;

        using var font = new Font(_opt.FontName, _opt.FontSizePt, FontStyle.Regular, GraphicsUnit.Point);
        using var brush = Brushes.Black;

        float X(float cm) => cm * 10f + _opt.OffsetXmm;
        float Y(float cm) => cm * 10f + _opt.OffsetYmm;

        // 1) 公司名稱（兩次）
        DrawCol(g, font, brush, d.CName, X(18.2f), Y(3.9f));
        DrawCol(g, font, brush, d.CName, X(14.3f), Y(8.3f));

        // 2) 會籍編號（旋轉直排）
        DrawCol(g, font, brush, d.Number, X(19.3f), Y(23.0f));

        // 3) 比價證明書有效日期（民國年/月/日）
        var (vy, vm, vd) = DateParts.TryParseRocOrIso(d.ProveValidDate);
        DrawCol(g, font, brush, ChineseNumerals.Translate(vy), X(15.8f), Y(9.3f));
        DrawCol(g, font, brush, ChineseNumerals.Translate(vm), X(15.8f), Y(12.6f));
        DrawCol(g, font, brush, ChineseNumerals.Translate(vd), X(15.8f), Y(15.5f));

        // 4) 入會日 / cdate
        var (jy, jm, jd) = DateParts.TryParseRocOrIso(d.JoinOrCDate);
        DrawCol(g, font, brush, ChineseNumerals.Translate(jy), X(13.5f), Y(7.8f));
        DrawCol(g, font, brush, ChineseNumerals.Translate(jm), X(13.5f), Y(10.2f));
        DrawCol(g, font, brush, ChineseNumerals.Translate(jd), X(13.5f), Y(12.4f));

        // 5) 工廠地址
        DrawCol(g, font, brush, d.FAddress, X(12.8f), Y(7.0f));

        // 6) 職稱 + 負責人 + 稱謂
        var who = $"{d.Title} {d.Chief} {SexWord(d.Sex)}".Trim();
        DrawCol(g, font, brush, who, X(12.0f), Y(8.3f));

        // 7) 工廠登記 字/號
        DrawCol(g, font, brush, d.FactoryRegPrefix, X(10.0f), Y(13.0f));
        DrawCol(g, font, brush, d.FactoryRegNo, X(10.0f), Y(15.2f));

        // 8) 資本額
        DrawCol(g, font, brush, d.Money, X(8.4f), Y(10.7f));

        // 9) 設備欄（drawarea）
        DrawArea(g, font, brush, d.EquipmentText, X(7.2f), Y(3.0f));

        // 10) 列印日（民國）
        var p = d.PrintDate;
        var rocYear = p.Year - 1911;
        DrawCol(g, font, brush, ChineseNumerals.Translate(rocYear), X(2.0f), Y(5.6f));
        DrawCol(g, font, brush, ChineseNumerals.Translate(p.Month), X(2.0f), Y(9.0f));
        DrawCol(g, font, brush, ChineseNumerals.Translate(p.Day), X(2.0f), Y(12.0f));
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

        // 用固定行距（mm）更穩，不受 DPI 影響；你之後要微調也好調
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
