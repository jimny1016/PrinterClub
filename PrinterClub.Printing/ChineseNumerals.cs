namespace PrinterClub.Printing;

internal static class ChineseNumerals
{
    public static string Translate(int input)
    {
        // 對齊 Java：0 或 >=10000 回傳空字串
        if (input == 0) return "";
        if (input >= 10000) return "";

        string[] num = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
        string[] unit = { "", "十", "百", "千" };

        var v = input;
        var digits = new int[4];
        digits[0] = (v / 1000) % 10;
        digits[1] = (v / 100) % 10;
        digits[2] = (v / 10) % 10;
        digits[3] = v % 10;

        // 照 Java 的組字風格：千百十個
        var sb = new System.Text.StringBuilder();

        for (int k = 0; k < 4; k++)
        {
            int d = digits[k];
            int pos = 3 - k;

            if (d == 0)
            {
                // Java 在某些情況會補零，這裡簡化：不主動補零
                continue;
            }

            // 十位特殊：10~19 常省略「一」
            if (pos == 1 && d == 1 && sb.Length == 0)
            {
                sb.Append("十");
                continue;
            }

            sb.Append(num[d]);
            sb.Append(unit[pos]);
        }

        return sb.ToString();
    }
}
