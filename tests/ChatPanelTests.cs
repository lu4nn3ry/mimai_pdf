using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using TradutorPdfOllama;

internal static class ChatPanelTests
{
    private sealed class FakeOllama : OllamaClient
    {
        internal Action<string> Chunk;
        internal Action Complete;
        internal string Prompt;
        public override void StreamGenerate(string model, string prompt, string systemPrompt,
            Action<string> onChunkReceived, Action onCompleted, Action<Exception> onError,
            CancellationTokenSource cts, List<string> images = null)
        { Prompt = prompt; Chunk = onChunkReceived; Complete = onCompleted; }
        internal void Reply(string text) { Chunk(text); Complete(); Application.DoEvents(); }
    }

    internal static void Run()
    {
        var fake = new FakeOllama();
        using (var form = new Form { Opacity = 0, ShowInTaskbar = false })
        using (var chat = new ChatPanel(fake))
        {
            form.Controls.Add(chat);
            form.Show();
            var input = Find<TextBox>(chat);
            var mode = Find<ComboBox>(chat);
            int edits = 0;
            string translation = "Tradução original $k$";
            chat.SetContext("p1", "Source", translation, "model", "Português", true);
            chat.TranslationEdited += (before, after) =>
            {
                if (before != translation) throw new Exception("Wrong edit snapshot.");
                translation = after;
                edits++;
                chat.SetContext("p1", "Source", translation, "model", "Português", true);
            };
            input.Text = "O que significa k?";
            chat.Send();
            if (!fake.Prompt.Contains("Source") || !fake.Prompt.Contains("Tradução original")) throw new Exception("Missing page context.");
            fake.Reply("**Comitê**: $k$ elementos.\n\n$$\\frac{1}{2}$$\n\n`$literal$`\n\n<script>alert(1)</script>");
            var viewer = Find<WebBrowser>(chat);
            var wait = System.Diagnostics.Stopwatch.StartNew();
            while (wait.ElapsedMilliseconds < 5000 && (viewer.Document == null || viewer.Document.Images.Count != 2))
            { Application.DoEvents(); Thread.Sleep(10); }
            if (viewer.Document == null || viewer.Document.Images.Count != 2 ||
                viewer.Document.GetElementsByTagName("b").Count < 1 ||
                viewer.Document.GetElementsByTagName("script").Count != 0 ||
                !viewer.Document.Body.InnerText.Contains("$literal$"))
                throw new Exception("Chat Markdown/math rendering or HTML escaping failed.");
            Console.WriteLine("PASS: chat renders bold and formula images, preserves code and escapes HTML.");
            if (edits != 0) throw new Exception("Question changed translation.");
            mode.SelectedIndex = 1;
            input.Text = "Troque original por revisada";
            chat.Send();
            fake.Reply("Tradução revisada $k$");
            if (edits != 1 || translation != "Tradução revisada $k$") throw new Exception("Edit not applied.");
            foreach (Control control in All(chat))
                if (control is Button && control.Text == "Desfazer") ((Button)control).PerformClick();
            if (translation != "Tradução original $k$" || edits != 2) throw new Exception("Undo failed.");
            input.Text = "Edite novamente";
            chat.Send();
            chat.SetContext("p2", "Other source", "Other translation", "model", "Português", true);
            fake.Reply("Late response");
            if (edits != 2) throw new Exception("Late edit crossed page boundaries.");
            input.Text = "Edite";
            chat.Send();
            chat.Cancel();
            fake.Reply("Cancelled response");
            if (edits != 2) throw new Exception("Cancelled edit applied.");
        }
        using (var form = new MainForm(false) { Opacity = 0, ShowInTaskbar = false })
        {
            form.Show();
            form.Size = form.MinimumSize;
            Application.DoEvents();
            if (Find<ChatPanel>(form).Width < 240) throw new Exception("Chat column is too narrow.");
        }
        Console.WriteLine("PASS: chat context, questions, edits, undo, cancellation, stale responses and three-column layout.");
    }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (Control nested in All(child)) yield return nested; }
    }
    private static T Find<T>(Control root) where T : Control
    {
        foreach (Control control in All(root)) if (control is T) return (T)control;
        throw new Exception("Control not found: " + typeof(T));
    }
}
