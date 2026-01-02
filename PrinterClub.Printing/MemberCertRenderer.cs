using PrinterClub.Data;
using System;
using System.Drawing;

namespace PrinterClub.Printing;

internal sealed class MemberCertRenderer
{
    private readonly PrintOptions _opt;

    public MemberCertRenderer(PrintOptions opt)
    {
        _opt = opt;
    }

    public void Render(Graphics g, MemberCertPrintData d)
    {
        // 你先維持 mm 模式（之後如果還會炸，再改成 Pixel + mm->px）
        g.PageUnit = GraphicsUnit.Millimeter;
        g.PageScale = 1f;

        using var font18 = CreateFontSafe(_opt.FontName, 18);
        using var font16 = CreateFontSafe(_opt.FontName, 16);
        using var font12 = CreateFontSafe(_opt.FontName, 12);
        using var brush = Brushes.Black;

        float X(float cm) => cm * 10f + _opt.OffsetXmm;
        float Y(float cm) => cm * 10f + _opt.OffsetYmm;

        // A) 會籍編號（橫排）
        DrawColRotated(g, font16, brush, d.Number, X(16.0f), Y(5.3f));

        // B) 公司名稱（直排，最多兩行）
        DrawColRotated(g, font18, brush, d.CName, X(4.8f), Y(9.5f), stepMm: 6f, maxCharsPerCol: 12);

        // C) 負責人 + 稱謂
        var who = $"{d.Chief}{SexWord(d.Sex)}";
        DrawColRotated(g, font18, brush, who, X(9.8f), Y(14.4f), stepMm: 6f);

        // D) 工廠地址（直排，數字不旋轉）
        DrawColRotated(g, font16, brush, d.FAddress, X(9.8f), Y(16.8f), stepMm: 6f);

        // E) 資本額（直排，10 字換行）
        DrawColRotated(g, font16, brush, d.Money, X(12.6f), Y(19.3f), stepMm: 6f, maxCharsPerCol: 10);

        // F) 列印日（今天，中文數字）
        var p = d.PrintDate;
        var roc = p.Year - 1911;
        DrawColRotated(g, font18, brush, ChineseNumerals.Translate(roc), X(9.5f), Y(26.6f), stepMm: 6f);
        DrawColRotated(g, font18, brush, ChineseNumerals.Translate(p.Month), X(13.0f), Y(26.6f), stepMm: 6f);
        DrawColRotated(g, font18, brush, ChineseNumerals.Translate(p.Day), X(16.6f), Y(26.6f), stepMm: 6f);

        // G) 會員證書有效日期（使用者輸入，阿拉伯數字）
        var (vy, vm, vd) = DateParts.TryParseRocOrIso(d.CertValidDate);
        DrawColRotated(g, font12, brush, vy.ToString(), X(14.6f), Y(27.8f), stepMm: 4.8f);
        DrawColRotated(g, font12, brush, vm.ToString(), X(16.2f), Y(27.8f), stepMm: 4.8f);
        DrawColRotated(g, font12, brush, vd.ToString(), X(17.6f), Y(27.8f), stepMm: 4.8f);
    }

    // ===== helpers =====

    private static Font CreateFontSafe(string fontName, float sizePt)
    {
        try
        {
            return new Font(fontName, sizePt, FontStyle.Regular, GraphicsUnit.Point);
        }
        catch
        {
            return new Font(FontFamily.GenericSansSerif, sizePt, FontStyle.Regular, GraphicsUnit.Point);
        }
    }

    private static string SexWord(string sex) =>
        sex switch
        {
            "F" or "f" => "女士",
            "M" or "m" => "先生",
            _ => ""
        };

    private static void ThrowIfInvalid(Graphics g, float x, float y, string tag)
    {
        if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
            throw new ArgumentOutOfRangeException($"{tag}: invalid coordinate x={x}, y={y}");

        var e = g.Transform.Elements;
        for (int i = 0; i < e.Length; i++)
        {
            if (float.IsNaN(e[i]) || float.IsInfinity(e[i]))
                throw new ArgumentOutOfRangeException($"{tag}: invalid transform element[{i}]={e[i]}");
        }
    }

    private static void DrawText(Graphics g, Font f, Brush b, string text, float x, float y)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        ThrowIfInvalid(g, x, y, "DrawText");
        g.DrawString(text, f, b, x, y);
    }

    /// <summary>
    /// 直排：整串先旋轉 -90，再用 (0,yy) 畫；超過 maxCharsPerCol 就往左開新欄
    /// </summary>
    private static void DrawColRotated(
        Graphics g, Font f, Brush b,
        string text, float x, float y,
        float stepMm = 6f,
        int maxCharsPerCol = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var state = g.Save();
        try
        {
            // 進場先檢查（旋轉前）
            ThrowIfInvalid(g, x, y, "DrawColRotated(before)");

            g.TranslateTransform(x, y);
            g.RotateTransform(-90f);

            // 旋轉後再檢查一次（很多 driver 會在這裡才壞）
            ThrowIfInvalid(g, 0, 0, "DrawColRotated(after)");

            float yy = 0;
            int count = 0;

            foreach (var ch in text)
            {
                if (count >= maxCharsPerCol)
                {
                    yy = 0;
                    // 開新欄：往左移（在旋轉後座標系）
                    g.TranslateTransform(-stepMm * 2f, 0f);
                    count = 0;

                    ThrowIfInvalid(g, 0, 0, "DrawColRotated(newcol)");
                }

                // 這裡就是你現在一直炸的點：加檢查
                ThrowIfInvalid(g, 0, yy, "DrawColRotated(draw)");
                g.DrawString(ch.ToString(), f, b, 0f, yy);

                yy += stepMm;
                count++;
            }
        }
        finally
        {
            g.Restore(state);
        }
    }

    /// <summary>
    /// 仿 Java：整串直排（旋轉 -90），但數字與 '-' 不旋轉（保持橫向）
    /// </summary>
    private static void DrawAddressDigitsNotRotated(
        Graphics g, Font f, Brush b,
        string text, float x, float y,
        float stepMm = 6f)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // 建議先做簡單清洗，避免控制字元造成 glyph/driver 問題
        text = SanitizeText(text);

        var state = g.Save();
        try
        {
            ThrowIfInvalid(g, x, y, "DrawAddress(before)");

            // 進入直排座標系
            g.TranslateTransform(x, y);
            g.RotateTransform(-90f);

            ThrowIfInvalid(g, 0, 0, "DrawAddress(after)");

            float yy = 0f;

            foreach (var ch in text)
            {
                bool isDigitLike = char.IsDigit(ch) || ch == '-' || ch == '－';

                if (isDigitLike)
                {
                    // 在直排座標系的 (0, yy) 局部轉回橫向
                    var s2 = g.Save();
                    try
                    {
                        g.TranslateTransform(0f, yy);
                        g.RotateTransform(90f);

                        ThrowIfInvalid(g, 0, 0, "DrawAddress(digit.draw)");
                        g.DrawString(ch.ToString(), f, b, 0f, 0f);
                    }
                    finally
                    {
                        g.Restore(s2);
                    }
                }
                else
                {
                    ThrowIfInvalid(g, 0, yy, "DrawAddress(char.draw)");
                    g.DrawString(ch.ToString(), f, b, 0f, yy);
                }

                yy += stepMm;
            }
        }
        finally
        {
            g.Restore(state);
        }
    }

    private static string SanitizeText(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        // 去控制字元
        var filtered = new string(s.Where(ch => !char.IsControl(ch)).ToArray());
        // 常見字元正規化
        filtered = filtered.Replace('　', ' ').Replace('－', '-');
        return filtered;
    }
}
