using System.Drawing.Printing;

namespace PrinterClub.Data;

public sealed class PrintOptions
{
    // 由呼叫端決定是否指定印表機；不指定就用系統預設印表機
    public string? PrinterName { get; set; }

    // 校正偏移（mm）
    public float OffsetXmm { get; set; } = 0;
    public float OffsetYmm { get; set; } = 0;

    // 紙張尺寸（mm）- 你量的比價證明書
    public float PaperWidthMm { get; set; } = 213.5f;
    public float PaperHeightMm { get; set; } = 280f;

    // 是否橫印
    public bool Landscape { get; set; } = false;

    // 字型（可讓呼叫端覆蓋）
    public string FontName { get; set; } = "標楷體";
    public float FontSizePt { get; set; } = 12f;
    public PaperSize CreatePaperSize(string name, float widthMm, float heightMm)
    {
        int w = MmToHundredthInch(widthMm);
        int h = MmToHundredthInch(heightMm);
        return new PaperSize(name, w, h);
    }

    private static int MmToHundredthInch(float mm)
    {
        // 1 inch = 25.4 mm; PaperSize 單位 1/100 inch
        return (int)System.Math.Round(mm / 25.4f * 100f);
    }
}
