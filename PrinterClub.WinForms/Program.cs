using System;
using System.Windows.Forms;
using static System.Windows.Forms.DataFormats;

namespace PrinterClub.WinForms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}
