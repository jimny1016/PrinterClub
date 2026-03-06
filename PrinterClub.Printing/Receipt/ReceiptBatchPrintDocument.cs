using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;

namespace PrinterClub.Printing.Receipt
{
    internal sealed class ReceiptBatchPrintDocument : PrintDocument
    {
        private readonly IReadOnlyList<ReceiptPrintData> _items;
        private readonly ReceiptRenderer _renderer;
        private int _index;
        private bool _disposed;

        public ReceiptBatchPrintDocument(IReadOnlyList<ReceiptPrintData> items, PrintOptions options)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            if (_items.Count == 0) throw new ArgumentException("items is empty", nameof(items));
            if (options == null) throw new ArgumentNullException(nameof(options));

            _renderer = new ReceiptRenderer(options);

            PrintController = new StandardPrintController();

            if (!string.IsNullOrWhiteSpace(options.PrinterName))
            {
                PrinterSettings.PrinterName = options.PrinterName;
                if (!PrinterSettings.IsValid)
                    throw new InvalidOperationException($"印表機不存在或不可用：{options.PrinterName}");
            }

            // 紙張：14cm x 24cm（或依 options）
            DefaultPageSettings.PaperSize =
                options.CreatePaperSize("Receipt-14x24cm", options.PaperWidthMm, options.PaperHeightMm);

            DefaultPageSettings.Landscape = false;
            DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
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
                    $"Receipt Render 第 {_index + 1} 筆失敗（ReceiptNo={_items[_index]?.ReceiptNo}, Number={_items[_index]?.Number}）",
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
                if (disposing)
                {
                    _renderer.Dispose();
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}