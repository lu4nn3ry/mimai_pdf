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
                        IsScannedOrEmpty = string.IsNullOrWhiteSpace(content)
                    }
                };
            }

            byte[] fileBytes = File.ReadAllBytes(filePath);
            return ExtractFromPdfBytes(fileBytes);
        }

        private static List<PdfPageData> ExtractFromPdfBytes(byte[] bytes)
        {
            var pages = new List<PdfPageData>();
            string rawPdf = Encoding.Default.GetString(bytes);

            // Find all streams in PDF
            var streams = ExtractDecompressedStreams(bytes);

            // If we found streams, process them into pages
            // Check if there are page markers or group text into pages
            var pageTextList = new List<string>();

            // Find page object references
            var pageMatches = Regex.Matches(rawPdf, @"/Type\s*/Page\b");
            int estimatedPageCount = Math.Max(1, pageMatches.Count);

            var fullTextBuilder = new StringBuilder();
            var currentStreamTexts = new List<string>();

            foreach (var streamBytes in streams)
            {
                string text = ParseContentStream(streamBytes);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    currentStreamTexts.Add(text);
                }
            }

            if (currentStreamTexts.Count == 0)
            {
                // Fallback: search for strings in raw ASCII
                string fallbackText = ExtractRawAsciiStrings(rawPdf);
                pages.Add(new PdfPageData
                {
                    PageNumber = 1,
                    ExtractedText = fallbackText,
                    IsScannedOrEmpty = string.IsNullOrWhiteSpace(fallbackText)
                });
                return pages;
            }

            // Distribute extracted streams across detected pages
            if (estimatedPageCount <= 1 || currentStreamTexts.Count == 1)
            {
                string allText = string.Join("\r\n\r\n", currentStreamTexts.ToArray());
                pages.Add(new PdfPageData
                {
                    PageNumber = 1,
                    ExtractedText = CleanExtractedText(allText),
                    IsScannedOrEmpty = string.IsNullOrWhiteSpace(allText)
                });
            }
            else
            {
                // If stream count is close to page count, map 1:1 or group evenly
                int streamsPerPage = Math.Max(1, (int)Math.Ceiling((double)currentStreamTexts.Count / estimatedPageCount));
                int pageNum = 1;
                var currentSb = new StringBuilder();

                for (int i = 0; i < currentStreamTexts.Count; i++)
                {
                    currentSb.AppendLine(currentStreamTexts[i]);
                    if ((i + 1) % streamsPerPage == 0 || i == currentStreamTexts.Count - 1)
                    {
                        string pText = CleanExtractedText(currentSb.ToString());
                        pages.Add(new PdfPageData
                        {
                            PageNumber = pageNum++,
                            ExtractedText = pText,
                            IsScannedOrEmpty = string.IsNullOrWhiteSpace(pText)
                        });
                        currentSb.Length = 0;
                    }
                }
            }

            if (pages.Count == 0)
            {
                pages.Add(new PdfPageData
                {
                    PageNumber = 1,
                    ExtractedText = string.Empty,
                    IsScannedOrEmpty = true
                });
            }

            return pages;
        }

        private static List<byte[]> ExtractDecompressedStreams(byte[] fileBytes)
        {
            var results = new List<byte[]>();
            int index = 0;
            byte[] streamStartToken = Encoding.ASCII.GetBytes("stream");
            byte[] streamEndToken = Encoding.ASCII.GetBytes("endstream");

            while (index < fileBytes.Length)
            {
                int streamStart = FindPattern(fileBytes, streamStartToken, index);
                if (streamStart == -1) break;

                int dataStart = streamStart + 6;
                // Skip CRLF or LF after "stream"
                if (dataStart < fileBytes.Length && fileBytes[dataStart] == '\r') dataStart++;
                if (dataStart < fileBytes.Length && fileBytes[dataStart] == '\n') dataStart++;

                int streamEnd = FindPattern(fileBytes, streamEndToken, dataStart);
                if (streamEnd == -1) break;

                int length = streamEnd - dataStart;
                if (length > 0 && dataStart + length <= fileBytes.Length)
                {
                    byte[] streamData = new byte[length];
                    Buffer.BlockCopy(fileBytes, dataStart, streamData, 0, length);

                    // Try decompressing with Deflate (handling 2-byte zlib header)
                    byte[] decompressed = TryDecompressZlib(streamData);
                    if (decompressed != null)
                    {
                        results.Add(decompressed);
                    }
                    else
                    {
                        // Uncompressed stream
                        results.Add(streamData);
                    }
                }

                index = streamEnd + 9;
            }

            return results;
        }

        private static byte[] TryDecompressZlib(byte[] input)
        {
            if (input == null || input.Length < 6) return null;

            // Zlib streams usually start with 0x78 (0x78 0x9C, 0x78 0x01, 0x78 0xDA)
            try
            {
                int skip = 0;
                if (input[0] == 0x78 && (input[1] == 0x9C || input[1] == 0x01 || input[1] == 0xDA || input[1] == 0x5E))
                {
                    skip = 2; // Skip 2 zlib header bytes for .NET DeflateStream
                }

                using (var ms = new MemoryStream(input, skip, input.Length - skip))
                using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
                using (var outMs = new MemoryStream())
                {
                    byte[] buffer = new byte[4096];
                    int read;
                    while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        outMs.Write(buffer, 0, read);
                    }
                    return outMs.ToArray();
                }
            }
            catch
            {
                // Could be uncompressed or custom compression
                return null;
            }
        }

        private static string ParseContentStream(byte[] streamBytes)
        {
            string content = Encoding.Default.GetString(streamBytes);
            if (!content.Contains("BT") && !content.Contains("Tj") && !content.Contains("TJ"))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var matches = Regex.Matches(content, @"BT([\s\S]*?)ET");

            foreach (Match m in matches)
            {
                string block = m.Groups[1].Value;
                string blockText = ExtractTextFromBlock(block);
                if (!string.IsNullOrWhiteSpace(blockText))
                {
                    sb.AppendLine(blockText.Trim());
                }
            }

            if (sb.Length == 0)
            {
                // Try direct Tj / TJ without BT/ET
                string direct = ExtractTextFromBlock(content);
                if (!string.IsNullOrWhiteSpace(direct))
                {
                    sb.AppendLine(direct.Trim());
                }
            }

            return sb.ToString();
        }

        private static string ExtractTextFromBlock(string block)
        {
            var sb = new StringBuilder();

            // Match Tj strings: (text) Tj
            var tjMatches = Regex.Matches(block, @"\((.*?)\)\s*Tj", RegexOptions.Singleline);
            foreach (Match tm in tjMatches)
            {
                sb.Append(UnescapePdfString(tm.Groups[1].Value) + " ");
            }

            // Match TJ arrays: [(text) 120 (text)] TJ
            var tjArrayMatches = Regex.Matches(block, @"\[([\s\S]*?)\]\s*TJ");
            foreach (Match am in tjArrayMatches)
            {
                string arrayContent = am.Groups[1].Value;
                var innerMatches = Regex.Matches(arrayContent, @"\((.*?)\)|<([0-9A-Fa-f]+)>");
                foreach (Match im in innerMatches)
                {
                    if (im.Groups[1].Success)
                    {
                        sb.Append(UnescapePdfString(im.Groups[1].Value));
                    }
                    else if (im.Groups[2].Success)
                    {
                        sb.Append(DecodeHexString(im.Groups[2].Value));
                    }
                }
                sb.Append(" ");
            }

            // Match ' and " text positioning operators
            var apostropheMatches = Regex.Matches(block, @"\((.*?)\)\s*['""]");
            foreach (Match apm in apostropheMatches)
            {
                sb.AppendLine();
                sb.Append(UnescapePdfString(apm.Groups[1].Value) + " ");
            }

            return sb.ToString();
        }

        private static string UnescapePdfString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    char next = s[i];
                    switch (next)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case '(': sb.Append('('); break;
                        case ')': sb.Append(')'); break;
                        case '\\': sb.Append('\\'); break;
                        default:
                            if (char.IsDigit(next))
                            {
                                // Octal escape
                                string octal = "" + next;
                                if (i + 1 < s.Length && char.IsDigit(s[i + 1])) { octal += s[++i]; }
                                if (i + 1 < s.Length && char.IsDigit(s[i + 1])) { octal += s[++i]; }
                                try
                                {
                                    int charVal = Convert.ToInt32(octal, 8);
                                    sb.Append((char)charVal);
                                }
                                catch
                                {
                                    sb.Append(next);
                                }
                            }
                            else
                            {
                                sb.Append(next);
                            }
                            break;
                    }
                }
                else
                {
                    sb.Append(s[i]);
                }
            }
            return sb.ToString();
        }

        private static string DecodeHexString(string hex)
        {
            try
            {
                if (hex.Length % 2 != 0) hex += "0";
                byte[] bytes = new byte[hex.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                }
                return Encoding.Default.GetString(bytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ExtractRawAsciiStrings(string raw)
        {
            var sb = new StringBuilder();
            var matches = Regex.Matches(raw, @"\(([^\)]{3,})\)");
            foreach (Match m in matches)
            {
                string val = m.Groups[1].Value;
                if (!val.StartsWith("/") && !val.StartsWith("Adobe") && val.Length > 2)
                {
                    sb.AppendLine(UnescapePdfString(val));
                }
            }
            return sb.ToString();
        }

        private static string CleanExtractedText(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Remove multiple consecutive spaces while preserving line breaks
            string cleaned = Regex.Replace(input, @"[ \t]+", " ");
            cleaned = Regex.Replace(cleaned, @"(\r?\n){3,}", "\r\n\r\n");
            return cleaned.Trim();
        }

        private static int FindPattern(byte[] src, byte[] pattern, int startIndex)
        {
            int maxFirstCharSlot = src.Length - pattern.Length + 1;
            for (int i = startIndex; i < maxFirstCharSlot; i++)
            {
                if (src[i] != pattern[0]) continue;

                bool match = true;
                for (int j = pattern.Length - 1; j >= 1; j--)
                {
                    if (src[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }
    }
}
