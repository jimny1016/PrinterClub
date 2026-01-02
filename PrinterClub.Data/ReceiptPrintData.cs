using System;

namespace PrinterClub.Printing
{
    public sealed class ReceiptPrintData
    {
        // DB
        public string Number { get; set; } = "";        // 會籍編號
        public string CName { get; set; } = "";         // 公司名稱
        public string AreaClass { get; set; } = "";     // 所在地區別（area_class）

        // Form input
        public string ReceiptNo { get; set; } = "";     // 右上角「號」
        public DateTime PrintDate { get; set; } = DateTime.Now;

        // 起訖年月（使用者輸入）
        // 建議格式：YYY.MM 或 YYY/MM 或 YYYY-MM，都可
        public string StartYm { get; set; } = "";       // 起始年月
        public string EndYm { get; set; } = "";         // 結束年月

        // 金額（使用者輸入）
        public int Fee { get; set; } = 0;               // 會費
        public int NewJoinFee { get; set; } = 0;        // 新入會費
    }
}
