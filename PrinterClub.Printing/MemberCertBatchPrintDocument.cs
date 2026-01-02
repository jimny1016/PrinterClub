using PrinterClub.Data;
using System.Drawing.Printing;

namespace PrinterClub.Printing;

internal sealed class MemberCertBatchPrintDocument : PrintDocument
{
    private readonly IList<MemberCertPrintData> _items;
    private readonly MemberCertRenderer _renderer;
    private int _index;

    public MemberCertBatchPrintDocument(
        IList<MemberCertPrintData> items,
        PrintOptions opt)
    {
        _items = items;
        _renderer = new MemberCertRenderer(opt);

        PrinterSettings.PrinterName = opt.PrinterName;
        DefaultPageSettings.PaperSize =
            new PaperSize("A4", (int)(opt.PaperWidthMm * 100 / 25.4f),
                                 (int)(opt.PaperHeightMm * 100 / 25.4f));
    }

    protected override void OnPrintPage(PrintPageEventArgs e)
    {
        var data = _items[_index];
        _renderer.Render(e.Graphics, data);

        _index++;
        e.HasMorePages = _index < _items.Count;
    }
}
