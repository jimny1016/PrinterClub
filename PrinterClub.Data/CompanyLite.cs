public class CompanyLite
{
    public string Number { get; set; } = "";
    public string CName { get; set; } = "";
    public string EName { get; set; } = "";

    public string CAddress { get; set; } = "";
    public string FAddress { get; set; } = "";

    public string TaxId { get; set; } = "";
    public string Money { get; set; } = "";
    public string Area { get; set; } = "";

    public string CompanyRegDate { get; set; } = "";
    public string CompanyRegPrefix { get; set; } = "";
    public string CompanyRegNo { get; set; } = "";

    public string FactoryRegDate { get; set; } = "";
    public string FactoryRegPrefix { get; set; } = "";
    public string FactoryRegNo { get; set; } = "";

    // UI/CRUD 統一用 join_date
    public string ApplyDate { get; set; } = "";

    public string CTel { get; set; } = "";
    public string CFax { get; set; } = "";
    public string FTel { get; set; } = "";
    public string FFax { get; set; } = "";

    public string Chief { get; set; } = "";
    public string ContactPerson { get; set; } = "";
    public string Extension { get; set; } = "";

    public string MainProduct { get; set; } = "";
    public string Email { get; set; } = "";
    public string Http { get; set; } = "";

    public string Classify { get; set; } = "";
    public string AreaClass { get; set; } = "";

    public string EquipmentText { get; set; } = "";

    public string VDate { get; set; } = "";   // 證明書有效日期（比價證明書）

    public string VDate2 { get; set; } = "";  // 會員證書有效日期（會籍證明書）
}
