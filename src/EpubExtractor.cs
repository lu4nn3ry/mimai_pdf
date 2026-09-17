using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace TradutorPdfOllama
{
    public static class EpubExtractor
    {
        private const int MaxChunkChars = 5000;
        private const int MinChunkChars = 2500;

        private class ManifestItem
        {
            public string Id;
            public string Href;
            public string MediaType;
            public string Properties;
        }

        public static List<PdfPageData> ExtractDocument(string filePath)
        {
            var pages = new List<PdfPageData>();

            using (var archive = ZipFile.OpenRead(filePath))
            {
                string opfPath = FindOpfPath(archive);
                List<string> contentFiles = null;

                if (!string.IsNullOrEmpty(opfPath))
                {
                    contentFiles = ParseOpf(archive, opfPath);
                }

                if (contentFiles == null || contentFiles.Count == 0)
                {
                    contentFiles = FallbackFindHtmlFiles(archive);
                }

                int pageNumber = 1;
                foreach (string contentFile in contentFiles)
                {
                    ZipArchiveEntry entry = FindEntry(archive, contentFile);
                    if (entry == null) continue;

                    string rawHtml;
                    using (var stream = entry.Open())
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        rawHtml = reader.ReadToEnd();
                    }

                    string text = ConvertHtmlToText(rawHtml);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    List<string> chunks = SplitIntoPages(text);
                    foreach (string chunk in chunks)
                    {
                        pages.Add(new PdfPageData
                        {
                            PageNumber = pageNumber++,
                            ExtractedText = chunk,
                            TextLoaded = true,
                            IsScannedOrEmpty = string.IsNullOrWhiteSpace(chunk)
                        });
                    }
                }
            }

            if (pages.Count == 0)
            {
                pages.Add(new PdfPageData
                {
                    PageNumber = 1,
                    ExtractedText = "(Documento EPUB vazio ou sem texto extraível)",
                    TextLoaded = true,
                    IsScannedOrEmpty = true
                });
            }

            return pages;
        }

        public static byte[] ExtractCoverImage(string filePath)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(filePath))
                {
                    string opfPath = FindOpfPath(archive);
                    if (!string.IsNullOrEmpty(opfPath))
                    {
                        string opfDir = GetDirectory(opfPath);
                        ZipArchiveEntry opfEntry = FindEntry(archive, opfPath);
                        if (opfEntry != null)
                        {
                            string opfXml;
                            using (var stream = opfEntry.Open())
                            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                            {
                                opfXml = reader.ReadToEnd();
                            }

                            var doc = new XmlDocument();
                            doc.LoadXml(opfXml);

                            var manifest = new Dictionary<string, ManifestItem>(StringComparer.OrdinalIgnoreCase);
                            XmlNodeList itemNodes = doc.GetElementsByTagName("item");
                            foreach (XmlNode node in itemNodes)
                            {
                                var elem = node as XmlElement;
                                if (elem == null) continue;
                                string id = elem.GetAttribute("id");
                                string href = elem.GetAttribute("href");
                                string media = elem.GetAttribute("media-type");
                                string props = elem.GetAttribute("properties");
                                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(href))
                                {
                                    manifest[id] = new ManifestItem { Id = id, Href = href, MediaType = media, Properties = props };
                                }
                            }

                            // 1. Check properties="cover-image" (EPUB 3)
                            foreach (var item in manifest.Values)
                            {
                                if (!string.IsNullOrEmpty(item.Properties) &&
                                    item.Properties.IndexOf("cover-image", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    byte[] bytes = ReadEntryBytes(archive, ResolvePath(opfDir, item.Href));
                                    if (bytes != null) return bytes;
                                }
                            }

                            // 2. Check <meta name="cover" content="id"/> (EPUB 2)
                            XmlNodeList metaNodes = doc.GetElementsByTagName("meta");
                            foreach (XmlNode node in metaNodes)
                            {
                                var elem = node as XmlElement;
                                if (elem == null) continue;
                                if (string.Equals(elem.GetAttribute("name"), "cover", StringComparison.OrdinalIgnoreCase))
                                {
                                    string coverId = elem.GetAttribute("content");
                                    if (!string.IsNullOrEmpty(coverId) && manifest.ContainsKey(coverId))
                                    {
                                        byte[] bytes = ReadEntryBytes(archive, ResolvePath(opfDir, manifest[coverId].Href));
                                        if (bytes != null) return bytes;
                                    }
                                }
                            }

                            // 3. Manifest item with id or href containing "cover" and image media type
                            foreach (var item in manifest.Values)
                            {
                                if (!string.IsNullOrEmpty(item.MediaType) && item.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (item.Id.IndexOf("cover", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        item.Href.IndexOf("cover", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        byte[] bytes = ReadEntryBytes(archive, ResolvePath(opfDir, item.Href));
                                        if (bytes != null) return bytes;
                                    }
                                }
                            }
                        }
                    }

                    // 4. Any zip entry with cover in name
                    foreach (var entry in archive.Entries)
                    {
                        string ext = Path.GetExtension(entry.FullName).ToLowerInvariant();
                        if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp")
                        {
                            if (entry.FullName.IndexOf("cover", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return ReadEntryBytes(archive, entry.FullName);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore cover extraction errors
            }

            return null;
        }

        private static string FindOpfPath(ZipArchive archive)
        {
            // First look for container.xml
            ZipArchiveEntry containerEntry = FindEntry(archive, "META-INF/container.xml");
            if (containerEntry != null)
            {
                try
                {
                    string xml;
                    using (var stream = containerEntry.Open())
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        xml = reader.ReadToEnd();
                    }

                    var doc = new XmlDocument();
                    doc.LoadXml(xml);
                    XmlNodeList rootfiles = doc.GetElementsByTagName("rootfile");
                    foreach (XmlNode node in rootfiles)
                    {
                        var elem = node as XmlElement;
                        if (elem != null && elem.HasAttribute("full-path"))
                        {
                            string path = elem.GetAttribute("full-path");
                            if (!string.IsNullOrEmpty(path)) return path;
                        }
                    }
                }
                catch { }
            }

            // Fallback: search for any .opf entry
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase))
                {
                    return entry.FullName;
                }
            }

            return null;
        }

        private static List<string> ParseOpf(ZipArchive archive, string opfPath)
        {
            var contentFiles = new List<string>();
            ZipArchiveEntry opfEntry = FindEntry(archive, opfPath);
            if (opfEntry == null) return contentFiles;

            try
            {
                string xml;
                using (var stream = opfEntry.Open())
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    xml = reader.ReadToEnd();
                }

                var doc = new XmlDocument();
                doc.LoadXml(xml);

                string opfDir = GetDirectory(opfPath);

                var manifest = new Dictionary<string, ManifestItem>(StringComparer.OrdinalIgnoreCase);
                XmlNodeList itemNodes = doc.GetElementsByTagName("item");
                foreach (XmlNode node in itemNodes)
                {
                    var elem = node as XmlElement;
                    if (elem == null) continue;
                    string id = elem.GetAttribute("id");
                    string href = elem.GetAttribute("href");
                    string media = elem.GetAttribute("media-type");
                    string props = elem.GetAttribute("properties");
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(href))
                    {
                        manifest[id] = new ManifestItem { Id = id, Href = href, MediaType = media, Properties = props };
                    }
                }

                XmlNodeList itemrefNodes = doc.GetElementsByTagName("itemref");
                foreach (XmlNode node in itemrefNodes)
                {
                    var elem = node as XmlElement;
                    if (elem == null) continue;
                    string idref = elem.GetAttribute("idref");
                    if (!string.IsNullOrEmpty(idref) && manifest.ContainsKey(idref))
                    {
                        string href = manifest[idref].Href;
                        string resolved = ResolvePath(opfDir, href);
                        if (!contentFiles.Contains(resolved))
                        {
                            contentFiles.Add(resolved);
                        }
                    }
                }

                if (contentFiles.Count == 0)
                {
                    // Fallback to manifest items if spine was empty
                    foreach (var item in manifest.Values)
                    {
                        if (IsHtmlMediaType(item.MediaType, item.Href))
                        {
                            string resolved = ResolvePath(opfDir, item.Href);
                            if (!contentFiles.Contains(resolved))
                            {
                                contentFiles.Add(resolved);
                            }
                        }
                    }
                }
            }
            catch { }

            return contentFiles;
        }

        private static List<string> FallbackFindHtmlFiles(ZipArchive archive)
        {
            var files = new List<string>();
            foreach (var entry in archive.Entries)
            {
                string ext = Path.GetExtension(entry.FullName).ToLowerInvariant();
                if (ext == ".xhtml" || ext == ".html" || ext == ".htm")
                {
                    files.Add(entry.FullName);
                }
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private static bool IsHtmlMediaType(string mediaType, string href)
        {
            if (!string.IsNullOrEmpty(mediaType))
            {
                string m = mediaType.ToLowerInvariant();
                if (m.Contains("xhtml") || m.Contains("html") || m.Contains("xml")) return true;
            }
            if (!string.IsNullOrEmpty(href))
            {
                string ext = Path.GetExtension(href).ToLowerInvariant();
                if (ext == ".xhtml" || ext == ".html" || ext == ".htm") return true;
            }
            return false;
        }

        private static string ConvertHtmlToText(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;

            // Remove head, script, style, comments
            html = Regex.Replace(html, @"<!--.*?-->", "", RegexOptions.Singleline);
            html = Regex.Replace(html, @"<head\b[^>]*>.*?</head>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<script\b[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style\b[^>]*>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Handle MathML: preserve LaTeX annotations or alttext
            html = Regex.Replace(html, @"<math\b[^>]*>(.*?)</math>", new MatchEvaluator(m =>
            {
                string mathBlock = m.Value;
                bool isBlock = mathBlock.IndexOf("display=\"block\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               mathBlock.IndexOf("display='block'", StringComparison.OrdinalIgnoreCase) >= 0;

                // Look for <annotation encoding="application/x-tex"> or "TeX"
                Match texMatch = Regex.Match(mathBlock, "<annotation\\b[^>]*encoding=[\"'](?:application/x-tex|TeX|text/x-tex)[\"'][^>]*>(.*?)</annotation>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (texMatch.Success)
                {
                    string tex = WebUtility.HtmlDecode(texMatch.Groups[1].Value.Trim());
                    return isBlock ? "\n\n$$" + tex + "$$\n\n" : " $" + tex + "$ ";
                }

                // Look for alttext attribute
                Match altMatch = Regex.Match(mathBlock, "alttext=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
                if (altMatch.Success)
                {
                    string alt = WebUtility.HtmlDecode(altMatch.Groups[1].Value.Trim());
                    return isBlock ? "\n\n$$" + alt + "$$\n\n" : " $" + alt + "$ ";
                }

                // Look for alt attribute
                Match altSimpleMatch = Regex.Match(mathBlock, "\\balt=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
                if (altSimpleMatch.Success)
                {
                    string alt = WebUtility.HtmlDecode(altSimpleMatch.Groups[1].Value.Trim());
                    return isBlock ? "\n\n$$" + alt + "$$\n\n" : " $" + alt + "$ ";
                }

                // Fallback: extract inner text of math elements
                string strippedMath = Regex.Replace(mathBlock, @"<[^>]+>", " ");
                strippedMath = WebUtility.HtmlDecode(strippedMath).Trim();
                return string.IsNullOrEmpty(strippedMath) ? "" : " $" + strippedMath + "$ ";
            }), RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Headings
            html = Regex.Replace(html, @"<h1\b[^>]*>(.*?)</h1>", "\n\n# $1\n\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<h2\b[^>]*>(.*?)</h2>", "\n\n## $1\n\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<h3\b[^>]*>(.*?)</h3>", "\n\n### $1\n\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<h4\b[^>]*>(.*?)</h4>", "\n\n#### $1\n\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<h5\b[^>]*>(.*?)</h5>", "\n\n##### $1\n\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<h6\b[^>]*>(.*?)</h6>", "\n\n###### $1\n\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Structural tags
            html = Regex.Replace(html, @"<blockquote\b[^>]*>(.*?)</blockquote>", "\n\n> $1\n\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<pre\b[^>]*>(.*?)</pre>", "\n\n```\n$1\n```\n\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<code\b[^>]*>(.*?)</code>", "`$1`", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<li\b[^>]*>(.*?)</li>", "\n* $1", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<hr\b[^>]*\/?>", "\n\n---\n\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<br\b[^>]*\/?>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<p\b[^>]*>", "\n\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</p>", "\n\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<div\b[^>]*>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</div>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<tr\b[^>]*>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<(td|th)\b[^>]*>(.*?)</\1>", " | $2", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</tr>", " |\n", RegexOptions.IgnoreCase);

            // Inline formatting
            html = Regex.Replace(html, @"<(b|strong)\b[^>]*>(.*?)</\1>", "**$2**", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<(i|em)\b[^>]*>(.*?)</\1>", "*$2*", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Images with alt
            html = Regex.Replace(html, @"<img\b[^>]*>", new MatchEvaluator(m =>
            {
                string tag = m.Value;
                Match altMatch = Regex.Match(tag, "alt=[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase);
                string alt = altMatch.Success ? altMatch.Groups[1].Value.Trim() : "";
                Match classMatch = Regex.Match(tag, "class=[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase);
                if (classMatch.Success && classMatch.Groups[1].Value.IndexOf("math", StringComparison.OrdinalIgnoreCase) >= 0 && !string.IsNullOrEmpty(alt))
                {
                    return " $" + alt + "$ ";
                }
                if (!string.IsNullOrEmpty(alt)) return "[Imagem: " + alt + "]";
                return "";
            }), RegexOptions.IgnoreCase);

            // Strip remaining HTML tags
            html = Regex.Replace(html, @"<[^>]+>", "");

            // Decode HTML entities
            html = WebUtility.HtmlDecode(html);

            // Normalize newlines and whitespace
            html = html.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = html.Split('\n');
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = Regex.Replace(lines[i], @"[ \t]+", " ").Trim();
                sb.Append(line).Append("\n");
            }

            string clean = Regex.Replace(sb.ToString(), @"\n{3,}", "\n\n").Trim();
            return clean;
        }

        private static List<string> SplitIntoPages(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            text = text.Replace("\r\n", "\n").Replace('\r', '\n');

            if (text.Length <= MaxChunkChars)
            {
                result.Add(text);
                return result;
            }

            string[] paragraphs = text.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            var currentChunk = new StringBuilder();

            for (int p = 0; p < paragraphs.Length; p++)
            {
                string para = paragraphs[p].Trim();
                if (string.IsNullOrEmpty(para)) continue;

                if (currentChunk.Length > 0 && (currentChunk.Length + para.Length + 2 > MaxChunkChars))
                {
                    result.Add(currentChunk.ToString().Trim());
                    currentChunk.Length = 0;
                }

                if (currentChunk.Length > 0) currentChunk.Append("\n\n");
                currentChunk.Append(para);
            }

            if (currentChunk.Length > 0)
            {
                result.Add(currentChunk.ToString().Trim());
            }

            return result;
        }

        private static ZipArchiveEntry FindEntry(ZipArchive archive, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            string normalized = relativePath.Replace('\\', '/').TrimStart('/');
            foreach (var entry in archive.Entries)
            {
                string entryName = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (string.Equals(entryName, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            return null;
        }

        private static byte[] ReadEntryBytes(ZipArchive archive, string relativePath)
        {
            ZipArchiveEntry entry = FindEntry(archive, relativePath);
            if (entry == null) return null;

            try
            {
                using (var stream = entry.Open())
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        private static string GetDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string normalized = path.Replace('\\', '/');
            int lastSlash = normalized.LastIndexOf('/');
            return lastSlash >= 0 ? normalized.Substring(0, lastSlash) : "";
        }

        private static string ResolvePath(string baseDir, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return "";

            // Strip fragment
            int hashIdx = relativePath.IndexOf('#');
            if (hashIdx >= 0) relativePath = relativePath.Substring(0, hashIdx);

            relativePath = Uri.UnescapeDataString(relativePath);

            if (string.IsNullOrEmpty(baseDir))
            {
                return relativePath.Replace('\\', '/').TrimStart('/');
            }

            string combined = baseDir.Replace('\\', '/').TrimEnd('/') + "/" + relativePath.Replace('\\', '/').TrimStart('/');
            string[] parts = combined.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var stack = new List<string>();

            foreach (var part in parts)
            {
                if (part == ".") continue;
                if (part == "..")
                {
                    if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                }
                else
                {
                    stack.Add(part);
                }
            }

            return string.Join("/", stack.ToArray());
        }
    }
}
