using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Windows.Forms;
using TradutorPdfOllama;

internal static class LazyPdfTests
{
    internal static void Run()
    {
        string path = Path.Combine(Path.GetTempPath(), "mimai-lazy-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            WritePdf(path);
            using (var document = new LazyPdfDocument(path))
            {
                AssertUnloaded(document);
                string selected = document.ExtractPageText(1);
                if (!selected.Contains("SELECTED_PAGE") || selected.Contains("OTHER_PAGE")) throw new Exception("Wrong PDF page extracted.");
                if (document.Pages[0].TextLoaded || document.Pages[2].TextLoaded) throw new Exception("Other pages were extracted.");
                document.Pages[1].ExtractedText = "Manually corrected";
                if (document.ExtractPageText(1) != "Manually corrected") throw new Exception("Text was unnecessarily re-extracted.");
                if (!string.IsNullOrWhiteSpace(document.ExtractPageText(2)) || !document.Pages[2].TextLoaded)
                    throw new Exception("Blank page was not preserved.");
            }
            using (var form = new MainForm(false) { Opacity = 0, ShowInTaskbar = false })
            {
                form.Show();
                Invoke(form, "LoadFile", path);
                var document = (LazyPdfDocument)Field(form, "_pdfDocument");
                AssertUnloaded(document);
                Invoke(form, "NavigatePage", 1);
                ((ToolStripComboBox)Field(form, "_cbMode")).SelectedIndex = 1;
                AssertUnloaded(document);
                // Vision consumes the selected image directly; it must not populate text.
                string imagePath = (string)Invoke(form, "GetCurrentRenderedImagePath");
                Directory.CreateDirectory(Path.GetDirectoryName(imagePath));
                using (var image = new System.Drawing.Bitmap(8, 8)) image.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
                var fake = new CaptureClient();
                typeof(MainForm).GetField("_ollama", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(form, fake);
                Invoke(form, "TranslateCurrentPage");
                if (!fake.UsedImages) throw new Exception("Vision did not send an image.");
                AssertUnloaded(document);
                Invoke(form, "CancelTranslation");
                ((ToolStripComboBox)Field(form, "_cbMode")).SelectedIndex = 0;
                Invoke(form, "TranslateCurrentPage");
                var wait = System.Diagnostics.Stopwatch.StartNew();
                while (fake.CallCount < 2 && wait.ElapsedMilliseconds < 5000)
                { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                if (fake.CallCount != 2 || fake.UsedImages || !fake.Prompt.Contains("SELECTED_PAGE") ||
                    !document.Pages[1].TextLoaded || document.Pages[0].TextLoaded || document.Pages[2].TextLoaded)
                    throw new Exception("Text mode did not lazily extract only the selected page before generation.");
                Invoke(form, "CancelTranslation");
                File.Delete(imagePath);
                Directory.Delete(Path.GetDirectoryName(imagePath));
            }
            Console.WriteLine("PASS: PDF open/navigation/Vision extract no text; selected-page extraction respects page-tree order, caches text and preserves blank pages.");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private sealed class CaptureClient : OllamaClient
    {
        internal bool UsedImages;
        internal int CallCount;
        internal string Prompt;
        public override void StreamGenerate(string model, string prompt, string systemPrompt, Action<string> chunk,
            Action completed, Action<Exception> error, System.Threading.CancellationTokenSource cts,
            System.Collections.Generic.List<string> images = null)
        { UsedImages = images != null && images.Count == 1; Prompt = prompt; CallCount++; }
    }

    private static void AssertUnloaded(LazyPdfDocument document)
    {
        if (document.Pages.Count != 3) throw new Exception("Wrong PDF page count.");
        foreach (var page in document.Pages)
            if (page.TextLoaded || page.ExtractedText != null) throw new Exception("PDF text was eagerly extracted.");
    }
    private static object Field(MainForm form, string name) { return typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form); }
    private static object Invoke(MainForm form, string method, params object[] args) { return typeof(MainForm).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, args); }

    private static void WritePdf(string path)
    {
        string[] objects = {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [4 0 R 3 0 R 5 0 R] /Count 3 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 9 0 R >> >> /Contents 6 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 9 0 R >> >> /Contents 7 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << >> /Contents 8 0 R >>",
            Stream("BT /F1 12 Tf 20 100 Td (SELECTED_PAGE) Tj ET"),
            Stream("BT /F1 12 Tf 20 100 Td (OTHER_PAGE) Tj ET"), Stream(""),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new System.Collections.Generic.List<int>();
        for (int i = 0; i < objects.Length; i++) { offsets.Add(pdf.Length); pdf.Append((i + 1) + " 0 obj\n" + objects[i] + "\nendobj\n"); }
        int xref = pdf.Length;
        pdf.Append("xref\n0 " + (objects.Length + 1) + "\n0000000000 65535 f \n");
        foreach (int offset in offsets) pdf.Append(offset.ToString("D10") + " 00000 n \n");
        pdf.Append("trailer\n<< /Size " + (objects.Length + 1) + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF");
        File.WriteAllText(path, pdf.ToString(), Encoding.ASCII);
    }
    private static string Stream(string text) { return "<< /Length " + text.Length + " >>\nstream\n" + text + "\nendstream"; }
}
