using System;
using System.Threading;
using System.Windows.Forms;

namespace TradutorPdfOllama
{
    public partial class MainForm
    {
        private void ExtractCurrentPageForTranslation()
        {
            var document = _pdfDocument;
            int index = _currentPageIndex;
            var request = new CancellationTokenSource();
            _cts = request;
            SetTranslatingState(true);
            _statusLabel.Text = "Extraindo texto apenas da página " + (index + 1) + "...";
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string text = document.ExtractPageText(index, request.Token);
                    PostPdfExtraction(request, () =>
                    {
                        if (_pdfDocument != document || _currentPageIndex != index) return;
                        var page = _pages[index];
                        page.ExtractedText = text;
                        page.TextLoaded = true;
                        page.IsScannedOrEmpty = string.IsNullOrWhiteSpace(text);
                        _updatingOriginalText = true;
                        try { _rtbOriginal.Text = text; _wbOriginal.Text = text; }
                        finally { _updatingOriginalText = false; }
                        _tabText.Text = "Texto Extraído";
                        _tabText.ToolTipText = "Texto da página atual";
                        SetTranslatingState(false);
                        if (page.IsScannedOrEmpty)
                        {
                            _isTranslatingAll = false;
                            _progressBar.Visible = false;
                            _statusLabel.Text = "Página sem texto extraível. Use OCR com IA Vision para traduzir a imagem.";
                            return;
                        }
                        TranslateCurrentPage();
                    });
                }
                catch (Exception ex)
                {
                    PostPdfExtraction(request, () =>
                    {
                        SetTranslatingState(false);
                        _isTranslatingAll = false;
                        _progressBar.Visible = false;
                        _statusLabel.Text = "Erro ao extrair a página selecionada: " + ex.Message;
                    });
                }
            });
        }

        private void PostPdfExtraction(CancellationTokenSource request, Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(() => { if (!IsDisposed && _cts == request && !request.IsCancellationRequested) action(); })); }
            catch (InvalidOperationException) { }
        }
    }
}
