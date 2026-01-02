using System;
using System.Collections.Generic;
using System.Text;

namespace PrinterClub.Data
{
    public sealed class MemberCertPrintData
    {
        public string Number { get; set; } = "";          // 會籍編號
        public string CName { get; set; } = "";           // 公司名稱
        public string Chief { get; set; } = "";           // 負責人
        public string Sex { get; set; } = "";             // M / F
        public string FAddress { get; set; } = "";        // 工廠地址
        public string Money { get; set; } = "";           // 資本額

        public string CertValidDate { get; set; } = "";   // ✅ 使用者輸入的「會員證書有效日期」

        public DateTime PrintDate { get; set; }            // 列印日（系統）
    }
}
