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
            return new ReceiptBatchPrintDocument(items, options);
        }
    }
}