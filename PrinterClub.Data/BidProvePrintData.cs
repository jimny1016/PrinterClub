namespace PrinterClub.Data;

public sealed class BidProvePrintData
{
    public string Number { get; set; } = "";
    public string CName { get; set; } = "";
    public string Money { get; set; } = "";
    public string FAddress { get; set; } = "";

    public string Title { get; set; } = "";
    public string Chief { get; set; } = "";
    public string Sex { get; set; } = ""; // M/F

    public string FactoryRegPrefix { get; set; } = ""; // factory_reg_prefix
    public string FactoryRegNo { get; set; } = "";     // factory_reg_no

    // 你要列印的兩個日期字串（呼叫端先決定格式、欄位來源）
    public string ProveValidDate { get; set; } = ""; // v_date，例: 107.12.31
    public string JoinOrCDate { get; set; } = "";    // join_date 或 c_date

    public string EquipmentText { get; set; } = "";

    public DateTime PrintDate { get; set; } = DateTime.Now;
}
