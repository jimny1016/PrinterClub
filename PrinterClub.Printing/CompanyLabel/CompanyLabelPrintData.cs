using System;

namespace PrinterClub.Data
{
    public sealed class CompanyLabelPrintData
    {
        public string Number { get; set; } = "";
        public string CName { get; set; } = "";
        public string CAddress { get; set; } = "";
        public string FAddress { get; set; } = "";
        public string AreaClass { get; set; } = "";
        public string Chief { get; set; } = "";
        public string Sex { get; set; } = "";
        public string ContactPerson { get; set; } = "";

        /// <summary>
        /// true = 印工廠地址；false = 印公司地址
        /// </summary>
        public bool UseFactoryAddress { get; set; }

        public string AddressToPrint =>
            UseFactoryAddress
                ? (FAddress ?? "").Trim()
                : (CAddress ?? "").Trim();

        /// <summary>
        /// 舊 Java 那種「負責人:xxx  聯絡人:yyy」合成字串
        /// </summary>
        public string ContactLine
        {
            get
            {
                var sexWord = Sex switch
                {
                    "F" or "f" => "女士",
                    "M" or "m" => "先生",
                    _ => ""
                };

                var chief = (Chief ?? "").Trim();
                var contact = (ContactPerson ?? "").Trim();

                if (string.IsNullOrWhiteSpace(chief) && string.IsNullOrWhiteSpace(contact))
                    return "";

                if (string.IsNullOrWhiteSpace(contact))
                    return $"負責人:{chief}{sexWord}";

                if (string.IsNullOrWhiteSpace(chief))
                    return $"聯絡人:{contact}";

                return $"負責人:{chief}{sexWord}  聯絡人:{contact}";
            }
        }
    }
}