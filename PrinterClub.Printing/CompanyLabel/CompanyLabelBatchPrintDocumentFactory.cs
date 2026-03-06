using PrinterClub.Data;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;

namespace PrinterClub.Printing
{
    public static class CompanyLabelBatchPrintDocumentFactory
    {
        public static PrintDocument Create(IReadOnlyList<CompanyLabelPrintData> items, PrintOptions options)
        {
            return new CompanyLabelBatchPrintDocument(items, options);
        }
    }
}