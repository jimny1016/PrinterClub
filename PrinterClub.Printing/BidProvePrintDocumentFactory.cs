using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;

namespace PrinterClub.Printing;

public static class BidProveBatchPrintDocumentFactory
{
    public static PrintDocument Create(IReadOnlyList<BidProvePrintData> items, PrintOptions options)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (items.Count == 0) throw new ArgumentException("items 不可為空", nameof(items));
        if (options == null) throw new ArgumentNullException(nameof(options));

        var doc = new PrintDocument
        {
            PrintController = new StandardPrintController()
        };

        // ✅ 先指定印表機（影響支援的 PaperSize/PrintableArea）
        if (!string.IsNullOrWhiteSpace(options.PrinterName))
        {
            doc.PrinterSettings.PrinterName = options.PrinterName;
            if (!doc.PrinterSettings.IsValid)
                throw new InvalidOperationException($"印表機不存在或不可用：{options.PrinterName}");
        }

        // ✅ 再設定頁面（Margins/PaperSize/Landscape）
        doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

        // 建自訂紙張（用你 PrintOptions 的換算也可以）
        var ps = options.CreatePaperSize("BidProve", options.PaperWidthMm, options.PaperHeightMm);
        doc.DefaultPageSettings.PaperSize = ps;

        doc.DefaultPageSettings.Landscape = options.Landscape;

        var renderer = new BidProveRenderer(options);
        var index = 0;

        doc.PrintPage += (_, e) =>
        {
            // ✅ 這裡加一層 try，讓你知道到底是哪一步炸掉
            try
            {
                renderer.Render(e.Graphics, items[index]);
            }
            catch (Exception ex)
            {
                // 把完整 stack 留在例外內（外層 DoPrint 的 ex.ToString() 才會看到）
                throw new InvalidOperationException($"Render 第 {index + 1} 筆失敗（Number={items[index]?.Number}）", ex);
            }

            index++;
            e.HasMorePages = index < items.Count;
        };

        return doc;
    }
}
