using PrinterClub.Data;
using System.Drawing.Printing;

namespace PrinterClub.Printing;

public static class BidProveBatchPrintDocumentFactory
{
    public static PrintDocument Create(IReadOnlyList<BidProvePrintData> items, PrintOptions options)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (items.Count == 0) throw new ArgumentException("items 不可為空", nameof(items));
        if (options == null) throw new ArgumentNullException(nameof(options));

        var doc = new PrintDocument();

        doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        doc.DefaultPageSettings.PaperSize = PaperSizeHelper.FromMillimeters(
            "BidProve",
            options.PaperWidthMm,
            options.PaperHeightMm
        );
        doc.DefaultPageSettings.Landscape = options.Landscape;

        if (!string.IsNullOrWhiteSpace(options.PrinterName))
        {
            doc.PrinterSettings.PrinterName = options.PrinterName;
            if (!doc.PrinterSettings.IsValid)
                throw new InvalidOperationException($"印表機不存在或不可用：{options.PrinterName}");
        }

        var renderer = new BidProveRenderer(options);
        var index = 0;

        doc.PrintPage += (_, e) =>
        {
            renderer.Render(e.Graphics, items[index]);

            index++;
            e.HasMorePages = index < items.Count;
        };

        return doc;
    }
}
