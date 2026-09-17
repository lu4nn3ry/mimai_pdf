using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using TradutorPdfOllama;

internal static class TranslationViewerTests
{
    [STAThread]
    private static int Main()
    {
        try
        {
            MathRenderer.Initialize();
            LazyPdfTests.Run();
            ChatPanelTests.Run();
            EpubTests.Run();
            using (var form = new Form { ShowInTaskbar = false, Opacity = 0 })
            using (var browser = MainForm.CreateTranslationViewer())
            {
                browser.Dock = DockStyle.Fill;
                form.Controls.Add(browser);
                form.Show();

                ShowAndExpect(browser, "Aguardando documento");
                ShowAndExpect(browser, "Traduzindo página");
                ShowAndExpect(browser, "Tradução concluída: equação $x_1 + x_2$.");
                Console.WriteLine("PASS: initial document is replaced by the translation.");

                for (int i = 0; i < 100; i++)
                    browser.DocumentText = "<html><body>Token " + i + "</body></html>";
                ShowAndExpect(browser, "Resultado completo após streaming");
                Console.WriteLine("PASS: final translation survives a burst of HTML updates.");

                ShowAndExpect(browser, "Tradução da segunda página em cache");
                ShowAndExpect(browser, "Tradução da primeira página em cache");
                Console.WriteLine("PASS: navigating between cached pages updates the viewer.");

                bool blocked = false;
                browser.Navigating += (s, e) =>
                {
                    if (e.Url != null && e.Url.Scheme == "http") blocked = e.Cancel;
                };
                browser.Navigate("http://127.0.0.1:1/blocked");
                WaitUntil(() => blocked, "External navigation was not blocked.");
                if (Read(browser) != "Tradução da primeira página em cache")
                    throw new Exception("External navigation replaced the translation.");
                Console.WriteLine("PASS: external navigation is blocked without clearing the translation.");
                TestMath(browser);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            return 1;
        }
        finally { MathRenderer.Cleanup(); }
    }

    private static void TestMath(WebBrowser browser)
    {
        string[] formulas = { "$k$", "$q \\le k/2$", "$q \\ge k/2 + 1$", "$x_1$", "$^{\\dagger 2}$", @"$$\frac{a}{b}$$", @"\(\alpha + \beta\)", @"\[\sum_{i=1}^{n} x_i\]" };
        foreach (string formula in formulas)
        {
            string html = MainForm.ConvertMarkdownToHtml(formula);
            if (!html.Contains("<img ")) throw new Exception("Formula was not rendered: " + formula + " => " + html);
            string expectedSource = System.Text.RegularExpressions.Regex.Match(html, "src='([^']+)'").Groups[1].Value;
            browser.DocumentText = "<html><body>" + html + "</body></html>";
            WaitUntil(() => browser.Document != null && browser.Document.Images.Count == 1 &&
                browser.Document.Images[0].GetAttribute("src") == expectedSource &&
                string.Equals(browser.Document.Images[0].GetAttribute("complete"), "true", StringComparison.OrdinalIgnoreCase) &&
                browser.Document.Images[0].OffsetRectangle.Width > 0, "Formula image did not load: " + formula);
            using (var bitmap = System.Drawing.Image.FromFile(new Uri(expectedSource).LocalPath))
                if (bitmap.Width < 1 || bitmap.Height < 1) throw new Exception("Invalid formula bitmap.");
        }
        Console.WriteLine("PASS: screenshot formulas, fractions, Greek symbols, sums and all four delimiters render as images.");
        string code = MainForm.ConvertMarkdownToHtml("`$k$`\n\n```tex\n$x_1$\n```");
        if (code.Contains("<img ") || !code.Contains("$x_1$")) throw new Exception("Code was interpreted as math.");
        string invalid = MainForm.ConvertMarkdownToHtml(@"$\notARealCommand{<script>}$");
        if (invalid.Contains("<script>") || !invalid.Contains("&lt;script&gt;")) throw new Exception("Invalid math fallback is not escaped.");
        Console.WriteLine("PASS: code remains literal and unsupported formulas fall back to escaped text.");
    }

    private static void ShowAndExpect(WebBrowser browser, string text)
    {
        browser.DocumentText = "<html><head><meta charset='UTF-8'></head><body>" + text + "</body></html>";
        WaitUntil(() => Read(browser) == text, "Viewer did not display: " + text);
    }

    private static string Read(WebBrowser browser)
    {
        return browser.Document == null || browser.Document.Body == null
            ? null : browser.Document.Body.InnerText;
    }

    private static void WaitUntil(Func<bool> condition, string failure)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < 5000)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        if (!condition()) throw new Exception(failure);
    }
}
