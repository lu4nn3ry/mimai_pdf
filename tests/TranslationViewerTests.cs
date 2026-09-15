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
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            return 1;
        }
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
