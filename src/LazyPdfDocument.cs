using System;
using System.Collections.Generic;
using System.Threading;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace TradutorPdfOllama
{
    internal sealed class LazyPdfDocument : IDisposable
    {
        private readonly PdfDocument _document;
        private readonly object _gate = new object();
        internal readonly List<PdfPageData> Pages = new List<PdfPageData>();

        internal LazyPdfDocument(string path)
        {
            // Opening indexes document structure only; GetPage is deferred until translation.
            _document = PdfDocument.Open(path);
            for (int i = 1; i <= _document.NumberOfPages; i++)
                Pages.Add(new PdfPageData { PageNumber = i });
        }

        internal string ExtractPageText(int pageIndex, CancellationToken cancellation = default(CancellationToken))
        {
            lock (_gate)
            {
                if (pageIndex < 0 || pageIndex >= Pages.Count) throw new ArgumentOutOfRangeException("pageIndex");
                cancellation.ThrowIfCancellationRequested();
                var page = Pages[pageIndex];
                if (!page.TextLoaded)
                {
                    string text = ContentOrderTextExtractor.GetText(_document.GetPage(pageIndex + 1));
                    cancellation.ThrowIfCancellationRequested();
                    page.ExtractedText = text;
                    page.IsScannedOrEmpty = string.IsNullOrWhiteSpace(page.ExtractedText);
                    page.TextLoaded = true;
                }
                return page.ExtractedText;
            }
        }

        public void Dispose() { lock (_gate) _document.Dispose(); }
    }
}
