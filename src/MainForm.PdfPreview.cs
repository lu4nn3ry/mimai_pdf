using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace TradutorPdfOllama
{
    public partial class MainForm
    {
        private string _previewSession = Guid.NewGuid().ToString("N");
        private readonly HashSet<string> _previewInFlight = new HashSet<string>();
        private Action _previewReady;

        private void RenderPdfPreview()
        {
            string outputPath = GetCurrentRenderedImagePath();
            if (outputPath == null) { SetPreviewImage(null); return; }
            if (_previewInFlight.Contains(outputPath)) return;
            if (File.Exists(outputPath)) { CompletePreview(outputPath, null); return; }

            string ext = Path.GetExtension(_currentFilePath);
            if (string.Equals(ext, ".epub", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    byte[] cover = EpubExtractor.ExtractCoverImage(_currentFilePath);
                    if (cover != null && cover.Length > 0)
                    {
                        File.WriteAllBytes(outputPath, cover);
                        CompletePreview(outputPath, null);
                        return;
                    }
                }
                catch { }
                SetPreviewImage(null);
                return;
            }

            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "render_pdf_page.ps1");
            if (!File.Exists(scriptPath)) { CompletePreview(outputPath, "Script de visualização não encontrado."); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            SetPreviewImage(null);
            _previewInFlight.Add(outputPath);
            int index = _currentPageIndex;
            string filePath = _currentFilePath;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string error = null;
                try
                {
                    var start = new System.Diagnostics.ProcessStartInfo {
                        FileName = "powershell.exe",
                        Arguments = string.Format("-NoProfile -ExecutionPolicy Bypass -File \"{0}\" -PdfPath \"{1}\" -PageIndex {2} -OutputPath \"{3}\"", scriptPath, filePath, index, outputPath),
                        UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true
                    };
                    using (var process = System.Diagnostics.Process.Start(start))
                    {
                        string stderr = "";
                        process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr += e.Data + "\n"; };
                        process.BeginErrorReadLine();
                        if (!process.WaitForExit(60000)) { try { process.Kill(); } catch { } throw new IOException("Tempo esgotado ao renderizar PDF."); }
                        process.WaitForExit();
                        if (process.ExitCode != 0) throw new IOException(stderr.Trim());
                    }
                }
                catch (Exception ex) { error = ex.Message; }
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke(new Action(() => { _previewInFlight.Remove(outputPath); CompletePreview(outputPath, error); })); }
                catch (InvalidOperationException) { }
            });
        }

        private void SetPreviewImage(Image image)
        {
            var previous = _pdfPreview.Image;
            _pdfPreview.Image = image;
            if (previous != null) previous.Dispose();
        }

        private void CompletePreview(string path, string error)
        {
            if (IsDisposed || path != GetCurrentRenderedImagePath()) return;
            try
            {
                if (error != null) throw new IOException(error);
                using (var file = File.OpenRead(path))
                using (var image = Image.FromStream(file)) SetPreviewImage(new Bitmap(image));
                var ready = _previewReady;
                _previewReady = null;
                if (ready != null) ready();
            }
            catch (Exception ex)
            {
                if (_previewReady != null)
                {
                    _previewReady = null;
                    SetTranslatingState(false);
                    _isTranslatingAll = false;
                    _progressBar.Visible = false;
                }
                _statusLabel.Text = "Erro na visualização PDF: " + ex.Message;
            }
        }
    }
}
