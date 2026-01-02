using System;
using System.Text.RegularExpressions;

namespace PrinterClub.Printing
{
    internal static class YearMonthParts
    {
        // 支援：
        // - 民國：107.12 / 107/12 / 107-12
        // - 西元：2020-12 / 2020.12 / 2020/12
        public static (int year, int month) TryParseRocOrIsoYm(string? s)
        {
            s = (s ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return (0, 0);

            var m = Regex.Match(s, @"^\s*(\d{2,4})\s*[./-]\s*(\d{1,2})\s*$");
            if (!m.Success) ToggleThrowYmFormat(s);

            var y = int.Parse(m.Groups[1].Value);
            var mm = int.Parse(m.Groups[2].Value);

            if (mm < 1 || mm > 12) ToggleThrowYmFormat(s);

            // 2~3位數年當民國
            if (y < 1911) return (y, mm);

            // 4位數年當西元 -> 轉民國
            return (y - 1911, mm);
        }

        private static void ToggleThrowYmFormat(string s)
        {
            throw new FormatException($"年月格式不正確：{s}（請用 107.12 或 2020-12 之類）");
        }
    }
}
