using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;

namespace PrinterClub.Printing;

internal sealed class MemberCertBatchPrintDocument : PrintDocument
{
    private readonly IList<MemberCertPrintData> _items;
    private readonly MemberCertRenderer _renderer;
    private int _index;
    private bool _disposed;

    public MemberCertBatchPrintDocument(
        IList<MemberCertPrintData> items,
        PrintOptions opt)
    {
        PrintController = new StandardPrintController();
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _renderer = new MemberCertRenderer(opt ?? throw new ArgumentNullException(nameof(opt)));

        if (!string.IsNullOrWhiteSpace(opt.PrinterName))
        {
            PrinterSettings.PrinterName = opt.PrinterName;
            if (!PrinterSettings.IsValid)
                throw new InvalidOperationException($"印表機不存在或不可用：{opt.PrinterName}");
        }

        DefaultPageSettings.PaperSize =
            new PaperSize("A4",
                (int)(opt.PaperWidthMm * 100 / 25.4f),
                (int)(opt.PaperHeightMm * 100 / 25.4f));
    }

    protected override void OnPrintPage(PrintPageEventArgs e)
    {
        if (_index >= _items.Count)
        {
            e.HasMorePages = false;
            return;
        }

        var data = _items[_index];
        _renderer.Render(e.Graphics, data);

        _index++;
        e.HasMorePages = _index < _items.Count;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _renderer.Dispose();
            }

            _disposed = true;
        }

        base.Dispose(disposing);
    }
}