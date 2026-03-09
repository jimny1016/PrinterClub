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

        // 依你目前量測：
        // 貼紙本體：11.9cm x 3.7cm
        // 左右洞洞紙：各 1cm
        //
        // 因為你是「長邊進印表機」，
        // 所以每張 label 的送紙節距應該是短邊 37mm。
        //
        // 整張紙寬度 = 119 + 10 + 10 = 139 mm
        // 每張紙高度 = 37 mm
        private const int PAPER_WIDTH_MM = 139;
        private const int PAPER_HEIGHT_MM = 37;

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

            OriginAtMargins = false;
            DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

            // 一頁就是一張貼紙的節距，不是整串紙的長度
            DefaultPageSettings.PaperSize =
                options.CreatePaperSize("CompanyLabel", PAPER_WIDTH_MM, PAPER_HEIGHT_MM);

            // 你現在文字方向已經正確，所以先不要翻 Landscape
            DefaultPageSettings.Landscape = false;
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
                // 補償印表機硬邊界，避免起印點被往內推
                // HardMargin 單位為 1/100 inch
                float hardMarginXpx = e.PageSettings.HardMarginX * e.Graphics.DpiX / 100f;
                float hardMarginYpx = e.PageSettings.HardMarginY * e.Graphics.DpiY / 100f;

                e.Graphics.TranslateTransform(-hardMarginXpx, -hardMarginYpx);

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