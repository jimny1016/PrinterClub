using PrinterClub.Data;
using System.Drawing.Printing;

namespace PrinterClub.Printing;

public static class MemberCertBatchPrintDocumentFactory
{
    public static PrintDocument Create(
        IList<MemberCertPrintData> items,
        PrintOptions options)
    {
        if (items == null || items.Count == 0)
            throw new ArgumentException("No data to print.");

        return new MemberCertBatchPrintDocument(items, options);
    }
}
