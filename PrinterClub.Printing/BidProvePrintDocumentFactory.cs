using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;

namespace PrinterClub.Printing;

public static class BidProveBatchPrintDocumentFactory
{
    public static PrintDocument Create(IReadOnlyList<BidProvePrintData> items, PrintOptions options)
    {
        return new BidProveBatchPrintDocument(items, options);
    }
}