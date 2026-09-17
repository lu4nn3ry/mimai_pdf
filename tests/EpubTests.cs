using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Text;
using TradutorPdfOllama;

internal static class EpubTests
{
    internal static void Run()
    {
        TestStandardEpub();
        TestMathAndEntityEpub();
        TestChapterSplitting();
        TestCoverExtraction();
        TestFallbackEpub();
        Console.WriteLine("PASS: EPUB document parsing, chapters, pagination, MathML, HTML entities, cover and fallbacks.");
    }

    private static void TestStandardEpub()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), "test_standard_" + Guid.NewGuid().ToString("N") + ".epub");
        try
        {
            using (var fs = new FileStream(tempPath, FileMode.Create))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddZipEntry(zip, "mimetype", "application/epub+zip");
                AddZipEntry(zip, "META-INF/container.xml",
                    "<?xml version='1.0'?>\n" +
                    "<container version='1.0' xmlns='urn:oasis:names:tc:opendocument:xmlns:container'>\n" +
                    "  <rootfiles>\n" +
                    "    <rootfile full-path='OEBPS/content.opf' media-type='application/oebps-package+xml'/>\n" +
                    "  </rootfiles>\n" +
                    "</container>");
                AddZipEntry(zip, "OEBPS/content.opf",
                    "<?xml version='1.0' encoding='utf-8'?>\n" +
                    "<package xmlns='http://www.idpf.org/2007/opf' version='2.0' unique-identifier='bookid'>\n" +
                    "  <metadata>\n" +
                    "    <dc:title>Test Book</dc:title>\n" +
                    "  </metadata>\n" +
                    "  <manifest>\n" +
                    "    <item id='ch1' href='chapter1.xhtml' media-type='application/xhtml+xml'/>\n" +
                    "    <item id='ch2' href='chapter2.xhtml' media-type='application/xhtml+xml'/>\n" +
                    "  </manifest>\n" +
                    "  <spine>\n" +
                    "    <itemref idref='ch1'/>\n" +
                    "    <itemref idref='ch2'/>\n" +
                    "  </spine>\n" +
                    "</package>");
                AddZipEntry(zip, "OEBPS/chapter1.xhtml",
                    "<?xml version='1.0' encoding='utf-8'?>\n" +
                    "<html xmlns='http://www.w3.org/1999/xhtml'>\n" +
                    "  <head><title>Chapter 1</title></head>\n" +
                    "  <body>\n" +
                    "    <h1>Capítulo 1</h1>\n" +
                    "    <p>Este é o primeiro parágrafo do livro de teste.</p>\n" +
                    "  </body>\n" +
                    "</html>");
                AddZipEntry(zip, "OEBPS/chapter2.xhtml",
                    "<?xml version='1.0' encoding='utf-8'?>\n" +
                    "<html xmlns='http://www.w3.org/1999/xhtml'>\n" +
                    "  <head><title>Chapter 2</title></head>\n" +
                    "  <body>\n" +
                    "    <h2>Capítulo 2</h2>\n" +
                    "    <p>Segundo capítulo com uma lista:</p>\n" +
                    "    <ul>\n" +
                    "      <li>Item Alfa</li>\n" +
                    "      <li>Item Beta</li>\n" +
                    "    </ul>\n" +
                    "  </body>\n" +
                    "</html>");
            }

            var pages = PdfExtractor.ExtractDocument(tempPath);
            if (pages.Count != 2) throw new Exception("Expected 2 pages for standard EPUB, got: " + pages.Count);
            if (!pages[0].ExtractedText.Contains("# Capítulo 1")) throw new Exception("Page 1 missing header: " + pages[0].ExtractedText);
            if (!pages[0].ExtractedText.Contains("Este é o primeiro parágrafo")) throw new Exception("Page 1 missing paragraph.");
            if (!pages[1].ExtractedText.Contains("## Capítulo 2")) throw new Exception("Page 2 missing header: " + pages[1].ExtractedText);
            if (!pages[1].ExtractedText.Contains("* Item Alfa")) throw new Exception("Page 2 missing list items.");
            if (!pages[0].TextLoaded || !pages[1].TextLoaded) throw new Exception("TextLoaded should be true.");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static void TestMathAndEntityEpub()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), "test_math_" + Guid.NewGuid().ToString("N") + ".epub");
        try
        {
            using (var fs = new FileStream(tempPath, FileMode.Create))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddZipEntry(zip, "META-INF/container.xml",
                    "<container version='1.0'><rootfiles><rootfile full-path='content.opf'/></rootfiles></container>");
                AddZipEntry(zip, "content.opf",
                    "<package version='3.0'>\n" +
                    "  <manifest><item id='math' href='math.xhtml' media-type='application/xhtml+xml'/></manifest>\n" +
                    "  <spine><itemref idref='math'/></spine>\n" +
                    "</package>");
                AddZipEntry(zip, "math.xhtml",
                    "<html><body>\n" +
                    "  <h1>Matemática &amp; Fórmulas</h1>\n" +
                    "  <p>Considere &quot;Euller&quot; &amp; a fórmula &lt;fundamental&gt;:</p>\n" +
                    "  <math display='block'>\n" +
                    "    <semantics>\n" +
                    "      <mrow><msup><mi>e</mi><mrow><mi>i</mi><mi>π</mi></mrow></msup><mo>+</mo><mn>1</mn><mo>=</mo><mn>0</mn></mrow>\n" +
                    "      <annotation encoding='application/x-tex'>e^{i\\pi} + 1 = 0</annotation>\n" +
                    "    </semantics>\n" +
                    "  </math>\n" +
                    "  <p>Também inline: <math alttext='x^2 + y^2 = r^2'><mi>x</mi></math>.</p>\n" +
                    "</body></html>");
            }

            var pages = EpubExtractor.ExtractDocument(tempPath);
            if (pages.Count != 1) throw new Exception("Expected 1 page, got: " + pages.Count);
            string text = pages[0].ExtractedText;

            if (!text.Contains("# Matemática & Fórmulas")) throw new Exception("Entities not decoded in title: " + text);
            if (!text.Contains("\"Euller\" & a fórmula <fundamental>")) throw new Exception("Entities not decoded in body: " + text);
            if (!text.Contains("$$e^{i\\pi} + 1 = 0$$")) throw new Exception("LaTeX block formula not preserved: " + text);
            if (!text.Contains("$x^2 + y^2 = r^2$")) throw new Exception("Inline math alttext not preserved: " + text);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static void TestChapterSplitting()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), "test_split_" + Guid.NewGuid().ToString("N") + ".epub");
        try
        {
            var longChapter = new StringBuilder();
            longChapter.Append("<html><body><h1>Capítulo Gigante</h1>\n");
            for (int i = 1; i <= 30; i++)
            {
                longChapter.Append("<p>Parágrafo ").Append(i).Append(": ");
                longChapter.Append(new string('X', 400));
                longChapter.Append("</p>\n");
            }
            longChapter.Append("</body></html>");

            using (var fs = new FileStream(tempPath, FileMode.Create))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddZipEntry(zip, "META-INF/container.xml",
                    "<container version='1.0'><rootfiles><rootfile full-path='content.opf'/></rootfiles></container>");
                AddZipEntry(zip, "content.opf",
                    "<package version='2.0'>\n" +
                    "  <manifest><item id='big' href='big.xhtml' media-type='application/xhtml+xml'/></manifest>\n" +
                    "  <spine><itemref idref='big'/></spine>\n" +
                    "</package>");
                AddZipEntry(zip, "big.xhtml", longChapter.ToString());
            }

            var pages = EpubExtractor.ExtractDocument(tempPath);
            if (pages.Count <= 1) throw new Exception("Expected large chapter to be split into multiple pages, got: " + pages.Count);
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].PageNumber != i + 1) throw new Exception("Invalid page number sequence.");
                if (pages[i].ExtractedText.Length > 7000) throw new Exception("Page chunk too large: " + pages[i].ExtractedText.Length);
            }
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static void TestCoverExtraction()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), "test_cover_" + Guid.NewGuid().ToString("N") + ".epub");
        try
        {
            byte[] imageBytes;
            using (var bmp = new Bitmap(50, 50))
            using (var ms = new MemoryStream())
            {
                using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Blue);
                bmp.Save(ms, ImageFormat.Png);
                imageBytes = ms.ToArray();
            }

            using (var fs = new FileStream(tempPath, FileMode.Create))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                AddZipEntry(zip, "META-INF/container.xml",
                    "<container version='1.0'><rootfiles><rootfile full-path='OEBPS/package.opf'/></rootfiles></container>");
                AddZipEntry(zip, "OEBPS/package.opf",
                    "<package version='3.0'>\n" +
                    "  <metadata><meta name='cover' content='cover-img'/></metadata>\n" +
                    "  <manifest>\n" +
                    "    <item id='cover-img' href='images/cover.png' media-type='image/png' properties='cover-image'/>\n" +
                    "    <item id='c1' href='c1.xhtml' media-type='application/xhtml+xml'/>\n" +
                    "  </manifest>\n" +
                    "  <spine><itemref idref='c1'/></spine>\n" +
                    "</package>");
                AddZipEntry(zip, "OEBPS/c1.xhtml", "<html><body><p>Livro com capa</p></body></html>");
                AddZipBinaryEntry(zip, "OEBPS/images/cover.png", imageBytes);
            }

            byte[] extractedCover = EpubExtractor.ExtractCoverImage(tempPath);
            if (extractedCover == null || extractedCover.Length != imageBytes.Length)
                throw new Exception("Cover image extraction failed or wrong size.");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static void TestFallbackEpub()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), "test_fallback_" + Guid.NewGuid().ToString("N") + ".epub");
        try
        {
            using (var fs = new FileStream(tempPath, FileMode.Create))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                // No container.xml, no content.opf; just plain html entries
                AddZipEntry(zip, "sectionA.html", "<html><body><p>Conteúdo da Seção A</p></body></html>");
                AddZipEntry(zip, "sectionB.html", "<html><body><p>Conteúdo da Seção B</p></body></html>");
            }

            var pages = EpubExtractor.ExtractDocument(tempPath);
            if (pages.Count != 2) throw new Exception("Fallback should find 2 HTML pages, got: " + pages.Count);
            if (!pages[0].ExtractedText.Contains("Conteúdo da Seção A")) throw new Exception("Fallback Page 1 incorrect.");
            if (!pages[1].ExtractedText.Contains("Conteúdo da Seção B")) throw new Exception("Fallback Page 2 incorrect.");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static void AddZipEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName);
        using (var stream = entry.Open())
        using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            writer.Write(content);
        }
    }

    private static void AddZipBinaryEntry(ZipArchive zip, string entryName, byte[] data)
    {
        var entry = zip.CreateEntry(entryName);
        using (var stream = entry.Open())
        {
            stream.Write(data, 0, data.Length);
        }
    }
}
