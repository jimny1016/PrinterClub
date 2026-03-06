using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;

namespace PrinterClub.Printing.BidProve;

internal sealed class BidProveBatchPrintDocument : PrintDocument
{
    private readonly IReadOnlyList<BidProvePrintData> _items;
    private readonly BidProveRenderer _renderer;
    private int _index;
    private bool _disposed;

    public BidProveBatchPrintDocument(IReadOnlyList<BidProvePrintData> items, PrintOptions options)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        if (_items.Count == 0) throw new ArgumentException("items 不可為空", nameof(items));

        _renderer = new BidProveRenderer(options ?? throw new ArgumentNullException(nameof(options)));

        PrintController = new StandardPrintController();

        if (!string.IsNullOrWhiteSpace(options.PrinterName))
        {
            PrinterSettings.PrinterName = options.PrinterName;
            if (!PrinterSettings.IsValid)
                throw new InvalidOperationException($"印表機不存在或不可用：{options.PrinterName}");
        }

        DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        DefaultPageSettings.PaperSize = options.CreatePaperSize("BidProve", options.PaperWidthMm, options.PaperHeightMm);
        DefaultPageSettings.Landscape = options.Landscape;
    }

    protected override void OnPrintPage(PrintPageEventArgs e)
    {
        if (_index >= _items.Count)
        {
            e.HasMorePages = false;
            return;
        }

        try
        {
            _renderer.Render(e.Graphics, _items[_index]);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Render 第 {_index + 1} 筆失敗（Number={_items[_index]?.Number}）",
                ex
            );
        }

        _index++;
        e.HasMorePages = _index < _items.Count;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}