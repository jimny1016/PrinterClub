using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;

namespace PrinterClub.Printing
{
    internal sealed class CompanyLabelBatchPrintDocument : PrintDocument
    {
        private readonly IReadOnlyList<CompanyLabelPrintData> _items;
        private readonly CompanyLabelRenderer _renderer;
        private int _index;
        private bool _disposed;

        public CompanyLabelBatchPrintDocument(IReadOnlyList<CompanyLabelPrintData> items, PrintOptions options)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            if (_items.Count == 0) throw new ArgumentException("items 不可為空", nameof(items));
            if (options == null) throw new ArgumentNullException(nameof(options));

            _renderer = new CompanyLabelRenderer(options);

            PrintController = new StandardPrintController();

            if (!string.IsNullOrWhiteSpace(options.PrinterName))
            {
                PrinterSettings.PrinterName = options.PrinterName;
                if (!PrinterSettings.IsValid)
                    throw new InvalidOperationException($"印表機不存在或不可用：{options.PrinterName}");
            }

            DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

            // 這裡先給一個可用的預設值：
            // 14cm x 24cm（你之後如果量到舊標籤實際尺寸，再改 options 即可）
            DefaultPageSettings.PaperSize =
                options.CreatePaperSize("CompanyLabel", options.PaperWidthMm, options.PaperHeightMm);

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
                    $"CompanyLabel Render 第 {_index + 1} 筆失敗（Number={_items[_index]?.Number}）",
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