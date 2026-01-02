using System.Drawing.Printing;

namespace PrinterClub.Printing;

internal static class PaperSizeHelper
{
    public static PaperSize FromMillimeters(string name, float widthMm, float heightMm)
    {
        // PaperSize: 1/100 inch
        int w = MmToHundredthsInch(widthMm);
        int h = MmToHundredthsInch(heightMm);
        return new PaperSize(name, w, h);
    }

    private static int MmToHundredthsInch(float mm)
    {
        // inch = mm / 25.4, then *100
        return (int)Math.Round(mm / 25.4f * 100f);
    }
}
