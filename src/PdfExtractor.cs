using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace TradutorPdfOllama
{
    public class PdfPageData
    {
        public int PageNumber { get; set; }
        public string ExtractedText { get; set; }
        public bool IsScannedOrEmpty { get; set; }
        public bool TextLoaded { get; set; }
    }

    public static class PdfExtractor
    {
        public static List<PdfPageData> ExtractDocument(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext != ".pdf")
            {
                // Plain text or markdown
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                return new List<PdfPageData>
                {
                    new PdfPageData
                    {
                        PageNumber = 1,
                        ExtractedText = content,
                        TextLoaded = true,
                        IsScannedOrEmpty = string.IsNullOrWhiteSpace(content)
                    }
                };
            }

            using (var document = new LazyPdfDocument(filePath)) return document.Pages;
        }

    }
}
