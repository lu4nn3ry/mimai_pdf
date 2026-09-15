using System;
using System.Windows.Forms;

namespace TradutorPdfOllama
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            MathRenderer.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new MainForm()); }
            finally { MathRenderer.Cleanup(); }
        }
    }
}
