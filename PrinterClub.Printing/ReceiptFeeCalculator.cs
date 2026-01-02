using System;
using System.Globalization;
using System.Linq;

namespace PrinterClub.Printing
{
    public static class ReceiptFeeCalculator
    {
        /// <summary>
        /// 依資本額 money（可能是 "60,000,000" 或 "60000000" 或含中文）推算「年會費」
        /// </summary>
        public static int CalcAnnualFeeFromMoney(string? moneyText)
        {
            long money = ParseMoneyToLong(moneyText);

            // ✅ 依你照片的級距（請你之後若有更精準規則，再改這裡即可）
            if (money <= 10_000) return 3000;
            if (money <= 30_000) return 5000;
            if (money <= 100_000_000) return 7000;
            return 10000;
        }

        /// <summary>
        /// 依起訖年月計算應繳常年會費（以年會費為基準，未滿一年以一年計）
        /// </summary>
        public static int CalcPeriodFee(int annualFee, string startYm, string endYm)
        {
            var (sy, sm) = YearMonthParts.TryParseRocOrIsoYm(startYm);
            var (ey, em) = YearMonthParts.TryParseRocOrIsoYm(endYm);

            if (sy <= 0 || sm <= 0 || ey <= 0 || em <= 0)
                throw new FormatException("起訖年月格式不正確。");

            int months = InclusiveMonths(sy, sm, ey, em);
            int years = (int)Math.Ceiling(months / 12.0);
            return annualFee * Math.Max(1, years);
        }

        /// <summary>
        /// 含頭含尾：107.01 ~ 107.12 => 12 個月
        /// </summary>
        public static int InclusiveMonths(int sy, int sm, int ey, int em)
        {
            int s = sy * 12 + (sm - 1);
            int e = ey * 12 + (em - 1);
            if (e < s) throw new ArgumentException("結束年月不可早於起始年月。");
            return (e - s) + 1;
        }

        public static long ParseMoneyToLong(string? moneyText)
        {
            if (string.IsNullOrWhiteSpace(moneyText)) return 0;

            // 只取數字
            var digits = new string(moneyText.Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return 0;

            if (long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;

            return 0;
        }
    }
}
