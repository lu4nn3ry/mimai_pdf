using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using WpfMath;

namespace TradutorPdfOllama
{
    internal static class MathRenderer
    {
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();
        private static string _imageDirectory;

        internal static void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (new AssemblyName(args.Name).Name != "WpfMath") return null;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Mimai.WpfMath.dll"))
                using (var bytes = new MemoryStream())
                {
                    if (stream == null) return null;
                    stream.CopyTo(bytes);
                    return Assembly.Load(bytes.ToArray());
                }
            };
        }

        internal static string Render(string delimitedLatex)
        {
            string cached;
            if (Cache.TryGetValue(delimitedLatex, out cached)) return cached;
            bool block = delimitedLatex.StartsWith("$$") || delimitedLatex.StartsWith(@"\[");
            int delimiter = delimitedLatex.StartsWith("$$") || delimitedLatex.StartsWith(@"\") ? 2 : 1;
            string latex = delimitedLatex.Substring(delimiter, delimitedLatex.Length - 2 * delimiter).Trim();
            try
            {
                cached = RenderImage(latex, block);
            }
            catch (Exception)
            {
                // Incomplete streaming expressions and unsupported TeX remain readable.
                cached = "<span class='math' title='Fórmula não suportada pelo renderizador local'>" + Escape(delimitedLatex) + "</span>";
            }
            if (Cache.Count < 2048) Cache[delimitedLatex] = cached;
            return cached;
        }

        private static string RenderImage(string latex, bool block)
        {
            if (latex.Length == 0 || latex.Length > 4096) throw new ArgumentException("Formula size");
            // Author footnotes commonly use a superscript without an explicit base.
            if (latex[0] == '^' || latex[0] == '_') latex = "{}" + latex;
            var formula = new TexFormulaParser().Parse(latex);
            var renderer = formula.GetRenderer(block ? TexStyle.Display : TexStyle.Text, 18.0, "Arial");
            if (renderer.RenderSize.Width > 4096 || renderer.RenderSize.Height > 2048)
                throw new ArgumentException("Formula dimensions");
            var bitmap = renderer.RenderToBitmap(0.0, 0.0);
            if (_imageDirectory == null)
            {
                _imageDirectory = Path.Combine(Path.GetTempPath(), "MimaiMath-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_imageDirectory);
            }
            string path = Path.Combine(_imageDirectory, Guid.NewGuid().ToString("N") + ".png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(path)) encoder.Save(file);
            string img = "<img class='math-image' src='" + new Uri(path).AbsoluteUri + "' alt='" + Escape(latex) + "' title='" + Escape(latex) + "'/>";
            return block ? "<div class='math-block'>" + img + "</div>" : img;
        }

        internal static string Escape(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        internal static void Cleanup()
        {
            Cache.Clear();
            if (_imageDirectory == null) return;
            try { Directory.Delete(_imageDirectory, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            _imageDirectory = null;
        }
    }
}
