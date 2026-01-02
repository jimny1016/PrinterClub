using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;

namespace PrinterClub.Printing
{
    public static class ReceiptBatchPrintDocumentFactory
    {
        public static PrintDocument Create(IReadOnlyList<ReceiptPrintData> items, PrintOptions options)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (items.Count == 0) throw new ArgumentException("items is empty", nameof(items));

            var renderer = new ReceiptRenderer(options);
            int idx = 0;

            var doc = new PrintDocument();

            if (!string.IsNullOrWhiteSpace(options.PrinterName))
                doc.PrinterSettings.PrinterName = options.PrinterName;

            // 紙張：你量 14cm x 24cm
            // 注意：PrintDocument 的 PaperSize 是 1/100 inch
            var paper = options.CreatePaperSize("Receipt-14x24cm", options.PaperWidthMm, options.PaperHeightMm);
            doc.DefaultPageSettings.PaperSize = paper;
            doc.DefaultPageSettings.Landscape = false;

            // 讓 driver 不要自己縮放（你之前已經在其他文件用得很順）
            doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

            doc.PrintPage += (s, e) =>
            {
                var d = items[idx];
                renderer.Render(e.Graphics, d);

                idx++;
                e.HasMorePages = idx < items.Count;
            };

            return doc;
        }
    }
}
