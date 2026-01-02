using PrinterClub.Data;
using System.Drawing.Printing;

namespace PrinterClub.Printing;

public static class BidProvePrintDocumentFactory
{
    public static PrintDocument Create(BidProvePrintData data, PrintOptions options)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (options == null) throw new ArgumentNullException(nameof(options));

        var doc = new PrintDocument();

        // 紙張、邊界
        doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        doc.DefaultPageSettings.PaperSize = PaperSizeHelper.FromMillimeters(
            "BidProve",
            options.PaperWidthMm,
            options.PaperHeightMm
        );
        doc.DefaultPageSettings.Landscape = options.Landscape;

        // 印表機（由呼叫端決定）
        if (!string.IsNullOrWhiteSpace(options.PrinterName))
        {
            doc.PrinterSettings.PrinterName = options.PrinterName;
            if (!doc.PrinterSettings.IsValid)
                throw new InvalidOperationException($"印表機不存在或不可用：{options.PrinterName}");
        }

        doc.PrintPage += (_, e) =>
        {
            var renderer = new BidProveRenderer(options);
            renderer.Render(e.Graphics, data);
            e.HasMorePages = false;
        };

        return doc;
    }
}
