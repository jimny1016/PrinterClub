using System;
using System.Text;

namespace PrinterClub.Printing
{
    internal static class ChineseMoneyUpper
    {
        private static readonly string[] Digit = { "零", "壹", "貳", "參", "肆", "伍", "陸", "柒", "捌", "玖" };
        private static readonly string[] Unit1 = { "", "拾", "佰", "仟" };
        private static readonly string[] Unit2 = { "", "萬", "億" };

        public static string ToUpper(int amount)
        {
            if (amount < 0) return "（負數）";
            if (amount == 0) return "零元整";

            var s = amount.ToString();
            // 分組：每4位一組（個/萬/億）
            int groupCount = (s.Length + 3) / 4;

            var sb = new StringBuilder();
            int idx = 0;
            for (int g = groupCount - 1; g >= 0; g--)
            {
                int len = s.Length - idx - g * 4;
                // len 會依序是：第一組可能 1~4，其餘固定 4
                if (len <= 0) continue;

                string part = s.Substring(idx, len);
                idx += len;

                string partZh = FourDigitsToUpper(part);
                if (!string.IsNullOrEmpty(partZh))
                {
                    sb.Append(partZh);
                    sb.Append(Unit2[g]);
                }
                else
                {
                    // 如果中間有空組，保留零的銜接由 FourDigitsToUpper 處理
                }
            }

            var result = NormalizeZero(sb.ToString());
            return result + "元整";
        }

        private static string FourDigitsToUpper(string part)
        {
            // part: 1~4 digits
            var sb = new StringBuilder();
            int n = part.Length;
            bool zeroPending = false;

            for (int i = 0; i < n; i++)
            {
                int d = part[i] - '0';
                int pos = n - 1 - i; // 0..3 對應 unit

                if (d == 0)
                {
                    zeroPending = true;
                    continue;
                }

                if (zeroPending)
                {
                    // 只有在前面真的有非零已輸出、且後面遇到非零才補零
                    if (sb.Length > 0) sb.Append("零");
                    zeroPending = false;
                }

                sb.Append(Digit[d]);
                sb.Append(Unit1[pos]);
            }

            return sb.ToString();
        }

        private static string NormalizeZero(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // 避免連續零
            while (s.Contains("零零")) s = s.Replace("零零", "零");
            // 去掉尾零
            if (s.EndsWith("零")) s = s.TrimEnd('零');
            return s;
        }
    }
}
