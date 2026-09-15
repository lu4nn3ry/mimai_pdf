using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TradutorPdfOllama
{
    public class MainForm : Form
    {
        // Core components
        private OllamaClient _ollama;
        private TranslationCache _cache;
        private List<PdfPageData> _pages;
        private int _currentPageIndex = 0;
        private string _currentFilePath = null;
        private CancellationTokenSource _cts = null;
        private bool _isTranslatingAll = false;
        private string _currentTranslation = "";
        private readonly System.Windows.Forms.Timer _translationRenderTimer = new System.Windows.Forms.Timer();

        // UI Controls
        private ToolStrip _toolStrip;
        private ToolStripButton _btnOpen;
        private ToolStripButton _btnOcr;
        private ToolStripSeparator _sep1;
        private ToolStripLabel _lblMode;
        private ToolStripComboBox _cbMode;
        private ToolStripSeparator _sepMode;
        private ToolStripLabel _lblModel;
        private ToolStripComboBox _cbModel;
        private ToolStripButton _btnRefreshModels;
        private ToolStripLabel _lblLang;
        private ToolStripComboBox _cbLang;
        private ToolStripSeparator _sep2;
        private ToolStripButton _btnTranslatePage;
        private ToolStripButton _btnTranslateAll;
        private ToolStripButton _btnStop;
        private ToolStripSeparator _sep3;
        private ToolStripButton _btnExportMd;
        private ToolStripButton _btnCopy;

        private Panel _navPanel;
        private Button _btnPrevPage;
        private Button _btnNextPage;
        private Label _lblPageInfo;
        private Label _lblOllamaStatus;
        private ProgressBar _progressBar;

        private SplitContainer _splitContainer;
        private Panel _leftPanel;
        private Panel _rightPanel;
        private Label _lblLeftHeader;
        private Label _lblRightHeader;
        private TabControl _leftTabs;
        private TabPage _tabPdf;
        private TabPage _tabText;
        private RichTextBox _wbOriginal;
        private RichTextBox _rtbOriginal;
        private PictureBox _pdfPreview;
        private WebBrowser _wbTranslated;

        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;
        private ToolStripStatusLabel _statusStats;

        // Mimai Pantone 2026 Color Palette
        private readonly Color _bgDark = Color.FromArgb(248, 250, 252);         // Slate 50 (Pantone Crisp White Base)
        private readonly Color _bgPanel = Color.FromArgb(255, 255, 255);        // Pure White Surface Card
        private readonly Color _bgText = Color.FromArgb(255, 255, 255);         // Pure White Input
        private readonly Color _fgText = Color.FromArgb(15, 23, 42);            // Slate 900 (High contrast primary text)
        private readonly Color _fgSecondary = Color.FromArgb(71, 85, 105);      // Slate 600 (Muted text)
        private readonly Color _borderSubtle = Color.FromArgb(226, 232, 240);   // Slate 200 (Hairline borders)
        private readonly Color _accentBlue = Color.FromArgb(2, 132, 199);       // Pantone 2026 Celestial Azure (Sky 600)
        private readonly Color _accentFeatherLight = Color.FromArgb(56, 189, 248); // Pantone Airy Cyan (Sky 400)
        private readonly Color _accentGreen = Color.FromArgb(16, 185, 129);     // Emerald Mint
        private readonly Color _accentAmber = Color.FromArgb(217, 119, 6);      // Amber Warm
        private readonly Color _accentRed = Color.FromArgb(225, 29, 72);        // Crimson Rose
        private readonly Color _headerBg = Color.FromArgb(241, 245, 249);       // Slate 100 (Header Badge Background)

        public MainForm()
        {
            InitializeComponent();
            _translationRenderTimer.Interval = 250;
            _translationRenderTimer.Tick += (s, e) =>
            {
                _translationRenderTimer.Stop();
                if (!string.IsNullOrEmpty(_currentTranslation)) RenderTranslation(_currentTranslation);
            };
            this.FormClosed += (s, e) => _translationRenderTimer.Dispose();
            _ollama = new OllamaClient();
            _cache = new TranslationCache();
            _pages = new List<PdfPageData>();

            LoadLanguages();
            CheckOllamaAndLoadModels();
        }

        private void InitializeComponent()
        {
            this.Text = "Mimai PDF — mim não traduz, mim faz tradução";
            this.Size = new Size(1180, 760);
            this.MinimumSize = new Size(840, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = _bgDark;
            this.ForeColor = _fgText;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            try
            {
                if (File.Exists("icon.ico"))
                {
                    this.Icon = new Icon("icon.ico");
                }
            }
            catch { }
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

            // 1. ToolStrip Modern Pantone 2026
            _toolStrip = new ToolStrip();
            _toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            _toolStrip.BackColor = _bgPanel;
            _toolStrip.ForeColor = _fgText;
            _toolStrip.Padding = new Padding(12, 6, 12, 6);
            _toolStrip.RenderMode = ToolStripRenderMode.Professional;
            _toolStrip.Renderer = new ToolStripProfessionalRenderer(new ModernPantoneColorTable());

            _btnOpen = new ToolStripButton("📂 Abrir");
            _btnOpen.ForeColor = _fgText;
            _btnOpen.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnOpen.Click += (s, e) => OpenFileDialogHandler();

            _btnOcr = new ToolStripButton("📷 OCR Imagem");
            _btnOcr.ForeColor = _accentBlue;
            _btnOcr.ToolTipText = "Reconhecer texto de imagem usando OCR nativo do Windows 11";
            _btnOcr.Click += (s, e) => OpenOcrDialogHandler();

            _sep1 = new ToolStripSeparator();

            _lblMode = new ToolStripLabel(" Modo:");
            _lblMode.ForeColor = _fgSecondary;
            _cbMode = new ToolStripComboBox();
            _cbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cbMode.Width = 175;
            _cbMode.Items.Add("Texto extraído (manual)");
            _cbMode.Items.Add("OCR com IA Vision");
            _cbMode.SelectedIndex = 0;
            _cbMode.ToolTipText = "Escolha entre enviar o texto extraído ou a imagem da página para um modelo Vision";
            _cbMode.SelectedIndexChanged += (s, e) => { if (_pages != null && _pages.Count > 0) UpdatePageView(); };
            _sepMode = new ToolStripSeparator();

            _lblModel = new ToolStripLabel(" Modelo IA:");
            _lblModel.ForeColor = _fgSecondary;

            _cbModel = new ToolStripComboBox();
            _cbModel.DropDownStyle = ComboBoxStyle.DropDownList;
            _cbModel.Width = 140;

            _btnRefreshModels = new ToolStripButton("🔄");
            _btnRefreshModels.ToolTipText = "Atualizar lista de modelos do Ollama";
            _btnRefreshModels.Click += (s, e) => CheckOllamaAndLoadModels();

            _lblLang = new ToolStripLabel(" Destino:");
            _lblLang.ForeColor = _fgSecondary;

            _cbLang = new ToolStripComboBox();
            _cbLang.DropDownStyle = ComboBoxStyle.DropDownList;
            _cbLang.Width = 140;

            _sep2 = new ToolStripSeparator();

            _btnTranslatePage = new ToolStripButton("▶ Traduzir Página");
            _btnTranslatePage.ForeColor = _accentBlue;
            _btnTranslatePage.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnTranslatePage.Click += (s, e) => TranslateCurrentPage();

            _btnTranslateAll = new ToolStripButton("⏩ Traduzir Tudo");
            _btnTranslateAll.ForeColor = _fgText;
            _btnTranslateAll.Click += (s, e) => TranslateAllPages();

            _btnStop = new ToolStripButton("⏹ Parar");
            _btnStop.ForeColor = _accentRed;
            _btnStop.Enabled = false;
            _btnStop.Click += (s, e) => CancelTranslation();

            _sep3 = new ToolStripSeparator();

            _btnExportMd = new ToolStripButton("💾 Exportar .MD");
            _btnExportMd.ForeColor = _fgSecondary;
            _btnExportMd.Click += (s, e) => ExportMarkdown();

            _btnCopy = new ToolStripButton("📋 Copiar Tradução");
            _btnCopy.ForeColor = _fgSecondary;
            _btnCopy.Click += (s, e) => CopyTranslation();

            _toolStrip.Items.AddRange(new ToolStripItem[] {
                _btnOpen, _btnOcr, _sep1,
                _lblMode, _cbMode, _sepMode,
                _lblModel, _cbModel, _btnRefreshModels,
                _lblLang, _cbLang, _sep2,
                _btnTranslatePage, _btnTranslateAll, _btnStop, _sep3,
                _btnExportMd, _btnCopy
            });

            // 2. Navigation Bar
            _navPanel = new Panel();
            _navPanel.Dock = DockStyle.Top;
            _navPanel.Height = 46;
            _navPanel.BackColor = _bgPanel;
            _navPanel.Padding = new Padding(12, 7, 12, 7);

            _btnPrevPage = new Button();
            _btnPrevPage.Text = "◀ Anterior";
            _btnPrevPage.Size = new Size(100, 30);
            _btnPrevPage.Location = new Point(12, 8);
            _btnPrevPage.FlatStyle = FlatStyle.Flat;
            _btnPrevPage.BackColor = _headerBg;
            _btnPrevPage.ForeColor = _fgText;
            _btnPrevPage.FlatAppearance.BorderSize = 1;
            _btnPrevPage.FlatAppearance.BorderColor = _borderSubtle;
            _btnPrevPage.Cursor = Cursors.Hand;
            _btnPrevPage.Click += (s, e) => NavigatePage(-1);

            _lblPageInfo = new Label();
            _lblPageInfo.Text = "Nenhum arquivo aberto";
            _lblPageInfo.AutoSize = true;
            _lblPageInfo.Location = new Point(124, 14);
            _lblPageInfo.ForeColor = _fgText;
            _lblPageInfo.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

            _btnNextPage = new Button();
            _btnNextPage.Text = "Próxima ▶";
            _btnNextPage.Size = new Size(100, 30);
            _btnNextPage.Location = new Point(280, 8);
            _btnNextPage.FlatStyle = FlatStyle.Flat;
            _btnNextPage.BackColor = _headerBg;
            _btnNextPage.ForeColor = _fgText;
            _btnNextPage.FlatAppearance.BorderSize = 1;
            _btnNextPage.FlatAppearance.BorderColor = _borderSubtle;
            _btnNextPage.Cursor = Cursors.Hand;
            _btnNextPage.Click += (s, e) => NavigatePage(1);

            _lblOllamaStatus = new Label();
            _lblOllamaStatus.Text = "● Ollama: Verificando...";
            _lblOllamaStatus.AutoSize = true;
            _lblOllamaStatus.Location = new Point(395, 14);
            _lblOllamaStatus.ForeColor = _accentAmber;
            _lblOllamaStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            _progressBar = new ProgressBar();
            _progressBar.Size = new Size(180, 18);
            _progressBar.Location = new Point(560, 14);
            _progressBar.Visible = false;

            _navPanel.Controls.AddRange(new Control[] {
                _btnPrevPage, _lblPageInfo, _btnNextPage, _lblOllamaStatus, _progressBar
            });

            // 3. Split View Panels
            _splitContainer = new SplitContainer();
            _splitContainer.Dock = DockStyle.Fill;
            _splitContainer.BackColor = _borderSubtle;
            _splitContainer.SplitterWidth = 5;

            // Left panel (Source)
            _leftPanel = new Panel();
            _leftPanel.Dock = DockStyle.Fill;
            _leftPanel.BackColor = _bgPanel;
            _leftPanel.Padding = new Padding(8);

            _lblLeftHeader = new Label();
            _lblLeftHeader.Text = "🪶 DOCUMENTO ORIGINAL";
            _lblLeftHeader.Dock = DockStyle.Top;
            _lblLeftHeader.Height = 30;
            _lblLeftHeader.BackColor = _headerBg;
            _lblLeftHeader.ForeColor = _accentBlue;
            _lblLeftHeader.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            _lblLeftHeader.TextAlign = ContentAlignment.MiddleLeft;
            _lblLeftHeader.Padding = new Padding(8, 0, 0, 0);

            _leftTabs = new TabControl();
            _leftTabs.Dock = DockStyle.Fill;
            _leftTabs.BackColor = _bgPanel;

            _tabPdf = new TabPage("Visualização PDF");
            _tabPdf.BackColor = _bgPanel;
            _wbOriginal = new RichTextBox();
            _wbOriginal.Dock = DockStyle.Fill;
            _wbOriginal.ReadOnly = true;
            _wbOriginal.BackColor = Color.White;
            _wbOriginal.ForeColor = Color.FromArgb(25, 25, 25);
            _wbOriginal.BorderStyle = BorderStyle.None;
            _wbOriginal.Font = new Font("Segoe UI", 11.0f, FontStyle.Regular);
            _wbOriginal.Padding = new Padding(28);

            _pdfPreview = new PictureBox();
            _pdfPreview.Dock = DockStyle.Fill;
            _pdfPreview.BackColor = _headerBg;
            _pdfPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _pdfPreview.Padding = new Padding(18);
            _pdfPreview.Visible = true;
            _tabPdf.Controls.Add(_pdfPreview);

            _tabText = new TabPage("Texto Extraído");
            _tabText.BackColor = _bgPanel;
            _rtbOriginal = new RichTextBox();
            _rtbOriginal.Dock = DockStyle.Fill;
            _rtbOriginal.BackColor = _bgPanel;
            _rtbOriginal.ForeColor = _fgText;
            _rtbOriginal.BorderStyle = BorderStyle.None;
            _rtbOriginal.Font = new Font("Consolas", 10.0f, FontStyle.Regular);
            _rtbOriginal.TextChanged += (s, e) => UpdateOriginalTextData();
            _tabText.Controls.Add(_rtbOriginal);

            _leftTabs.TabPages.AddRange(new TabPage[] { _tabPdf, _tabText });

            _leftPanel.Controls.Add(_leftTabs);
            _leftPanel.Controls.Add(_lblLeftHeader);

            // Right panel (Translation)
            _rightPanel = new Panel();
            _rightPanel.Dock = DockStyle.Fill;
            _rightPanel.BackColor = _bgPanel;
            _rightPanel.Padding = new Padding(8);

            _lblRightHeader = new Label();
            _lblRightHeader.Text = "🌐 TRADUÇÃO MIMAI (MARKDOWN & LATEX)";
            _lblRightHeader.Dock = DockStyle.Top;
            _lblRightHeader.Height = 30;
            _lblRightHeader.BackColor = _headerBg;
            _lblRightHeader.ForeColor = _accentBlue;
            _lblRightHeader.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            _lblRightHeader.TextAlign = ContentAlignment.MiddleLeft;
            _lblRightHeader.Padding = new Padding(8, 0, 0, 0);

            _wbTranslated = CreateTranslationViewer();
            _wbTranslated.Dock = DockStyle.Fill;
            _wbTranslated.BackColor = _bgPanel;

            _rightPanel.Controls.Add(_wbTranslated);
            _rightPanel.Controls.Add(_lblRightHeader);

            _splitContainer.Panel1.Controls.Add(_leftPanel);
            _splitContainer.Panel2.Controls.Add(_rightPanel);

            // 4. Status Strip
            _statusStrip = new StatusStrip();
            _statusStrip.BackColor = _bgPanel;
            _statusStrip.ForeColor = _fgSecondary;
            _statusStrip.Renderer = new ToolStripProfessionalRenderer(new ModernPantoneColorTable());

            _statusLabel = new ToolStripStatusLabel("Pronto. Arraste um arquivo PDF ou clique em 'Abrir'.");
            _statusLabel.Spring = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

            _statusStats = new ToolStripStatusLabel("0 palavras | 0 caracteres");
            _statusStats.TextAlign = ContentAlignment.MiddleRight;
            _statusStats.ForeColor = _accentBlue;

            _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _statusStats });

            // Layout
            this.Controls.Add(_splitContainer);
            this.Controls.Add(_navPanel);
            this.Controls.Add(_toolStrip);
            this.Controls.Add(_statusStrip);
        }

        private void LoadLanguages()
        {
            _cbLang.Items.Clear();
            _cbLang.Items.AddRange(new object[] {
                "Português (BR)",
                "English",
                "Español",
                "Français",
                "Deutsch",
                "Italiano",
                "日本語 (Japanese)",
                "中文 (Chinese)",
                "Русский (Russian)"
            });
            _cbLang.SelectedIndex = 0;
        }

        private void CheckOllamaAndLoadModels()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool isOnline = _ollama.CheckConnection();
                var models = isOnline ? _ollama.GetModels() : new List<string>();

                this.BeginInvoke(new Action(() =>
                {
                    if (isOnline)
                    {
                        _lblOllamaStatus.Text = "● Ollama: Online";
                        _lblOllamaStatus.ForeColor = _accentGreen;
                        _cbModel.Items.Clear();
                        if (models.Count > 0)
                        {
                            foreach (var m in models) _cbModel.Items.Add(m);
                            _cbModel.SelectedIndex = 0;
                            _statusLabel.Text = string.Format("Ollama conectado. {0} modelo(s) detectado(s).", models.Count);
                        }
                        else
                        {
                            _cbModel.Items.Add("qwen2.5:7b");
                            _cbModel.Items.Add("llama3.2:3b");
                            _cbModel.SelectedIndex = 0;
                            _statusLabel.Text = "Ollama conectado, mas nenhum modelo baixado ainda. Execute: ollama run qwen2.5:7b";
                        }
                    }
                    else
                    {
                        _lblOllamaStatus.Text = "○ Ollama: Offline";
                        _lblOllamaStatus.ForeColor = _accentRed;
                        _cbModel.Items.Clear();
                        _cbModel.Items.Add("qwen2.5:7b");
                        _cbModel.SelectedIndex = 0;
                        _statusLabel.Text = "Aviso: Ollama não encontrado em http://localhost:11434. Inicie o Ollama.";
                    }
                }));
            });
        }

        private void OpenFileDialogHandler()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Selecione um documento";
                ofd.Filter = "Documentos Suportados (*.pdf;*.txt;*.md)|*.pdf;*.txt;*.md|Arquivos PDF (*.pdf)|*.pdf|Arquivos de Texto (*.txt;*.md)|*.txt;*.md|Todos os Arquivos (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadFile(ofd.FileName);
                }
            }
        }

        private void OpenOcrDialogHandler()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Selecione uma imagem para OCR";
                ofd.Filter = "Imagens (*.png;*.jpg;*.jpeg;*.bmp;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.tiff|Todos os Arquivos (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    PerformOcrOnImage(ofd.FileName);
                }
            }
        }

        private void PerformOcrOnImage(string imagePath)
        {
            try
            {
                _statusLabel.Text = "Executando OCR nativo do Windows em " + Path.GetFileName(imagePath) + "...";
                this.Cursor = Cursors.WaitCursor;

                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ocr_windows.ps1");
                if (!File.Exists(scriptPath))
                {
                    scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr_windows.ps1");
                }

                string ocrText = "";
                if (File.Exists(scriptPath))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = string.Format("-NoProfile -ExecutionPolicy Bypass -File \"{0}\" -ImagePath \"{1}\"", scriptPath, imagePath),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    using (var proc = System.Diagnostics.Process.Start(psi))
                    {
                        ocrText = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(30000);
                    }
                }
                else
                {
                    ocrText = "[Aviso: Script ocr_windows.ps1 não encontrado para OCR de imagem.]";
                }

                _currentFilePath = imagePath;
                _pages = new List<PdfPageData>
                {
                    new PdfPageData
                    {
                        PageNumber = 1,
                        ExtractedText = ocrText.Trim(),
                        IsScannedOrEmpty = string.IsNullOrWhiteSpace(ocrText)
                    }
                };
                _currentPageIndex = 0;
                this.Cursor = Cursors.Default;

                UpdatePageView();
                _statusLabel.Text = "OCR concluído com sucesso!";
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                MessageBox.Show("Erro ao executar OCR: " + ex.Message, "Erro OCR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusLabel.Text = "Erro ao processar OCR.";
            }
        }

        private void LoadFile(string filePath)
        {
            try
            {
                _currentFilePath = filePath;
                _statusLabel.Text = "Extraindo conteúdo de " + Path.GetFileName(filePath) + "...";
                this.Cursor = Cursors.WaitCursor;

                _pages = PdfExtractor.ExtractDocument(filePath);
                _currentPageIndex = 0;
                this.Cursor = Cursors.Default;

                UpdatePageView();
                _statusLabel.Text = string.Format("Documento carregado: {0} ({1} página(s))", Path.GetFileName(filePath), _pages.Count);
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                MessageBox.Show("Erro ao abrir arquivo: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusLabel.Text = "Erro ao carregar documento.";
            }
        }

        private void UpdatePageView()
        {
            if (_pages == null || _pages.Count == 0)
            {
                _lblPageInfo.Text = "Nenhum arquivo aberto";
                _rtbOriginal.Clear();
                _wbOriginal.Clear();
                if (_pdfPreview != null) _pdfPreview.Image = null;
                _wbTranslated.DocumentText = GetEmptyStateHtml();
                _btnPrevPage.Enabled = false;
                _btnNextPage.Enabled = false;
                return;
            }

            if (_currentPageIndex < 0) _currentPageIndex = 0;
            if (_currentPageIndex >= _pages.Count) _currentPageIndex = _pages.Count - 1;

            var page = _pages[_currentPageIndex];
            _lblPageInfo.Text = string.Format("Página {0} de {1}", _currentPageIndex + 1, _pages.Count);
            _btnPrevPage.Enabled = _currentPageIndex > 0;
            _btnNextPage.Enabled = _currentPageIndex < _pages.Count - 1;

            _wbOriginal.Text = page.ExtractedText ?? string.Empty;
            _rtbOriginal.Text = page.ExtractedText ?? string.Empty;
            RenderPdfPreview();

            // Check cache for this page
            string model = GetCacheModelKey();
            string lang = _cbLang.SelectedItem != null ? _cbLang.SelectedItem.ToString() : "Português";
            string cachedTranslation = _cache.GetTranslation(_currentFilePath, _currentPageIndex + 1, lang, model);

            if (!string.IsNullOrEmpty(cachedTranslation))
            {
                RenderTranslation(cachedTranslation);
            }
            else
            {
                _wbTranslated.DocumentText = GetEmptyStateHtml();
            }

            UpdateStats();
        }

        private void RenderPdfPreview()
        {
            if (_pdfPreview == null || string.IsNullOrEmpty(_currentFilePath) ||
                !string.Equals(Path.GetExtension(_currentFilePath), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                if (_pdfPreview != null) _pdfPreview.Image = null;
                return;
            }

            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "render_pdf_page.ps1");
            if (!File.Exists(scriptPath)) return;
            string outputPath = GetCurrentRenderedImagePath();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            _statusLabel.Text = string.Format("Renderizando página {0}...", _currentPageIndex + 1);
            _pdfPreview.Image = null;

            int pageIndex = _currentPageIndex;
            string pdfPath = _currentFilePath;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = string.Format("-NoProfile -ExecutionPolicy Bypass -File \"{0}\" -PdfPath \"{1}\" -PageIndex {2} -OutputPath \"{3}\"", scriptPath, pdfPath, pageIndex, outputPath),
                        UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardOutput = true, RedirectStandardError = true
                    };
                    using (var proc = System.Diagnostics.Process.Start(psi))
                    {
                        if (!proc.WaitForExit(60000))
                        {
                            try { proc.Kill(); } catch { }
                            BeginInvoke(new Action(() => _statusLabel.Text = "Tempo esgotado ao renderizar a página PDF."));
                            return;
                        }
                        string renderError = proc.StandardError.ReadToEnd();
                        if (proc.ExitCode != 0)
                        {
                            string detail = string.IsNullOrWhiteSpace(renderError) ? "o renderizador terminou com erro." : renderError.Trim();
                            BeginInvoke(new Action(() => _statusLabel.Text = "Erro ao renderizar PDF: " + detail));
                            return;
                        }
                    }
                    if (File.Exists(outputPath))
                    {
                        using (var fs = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var temp = Image.FromStream(fs))
                        {
                            var image = new Bitmap(temp);
                            BeginInvoke(new Action(() =>
                            {
                                if (pageIndex == _currentPageIndex) { _pdfPreview.Image = image; _statusLabel.Text = "Visualização PDF pronta."; }
                                else image.Dispose();
                            }));
                        }
                    }
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(() => _statusLabel.Text = "Não foi possível renderizar o PDF: " + ex.Message));
                }
            });
        }

        private void NavigatePage(int delta)
        {
            if (_pages == null || _pages.Count == 0) return;
            int newIndex = _currentPageIndex + delta;
            if (newIndex >= 0 && newIndex < _pages.Count)
            {
                _currentPageIndex = newIndex;
                UpdatePageView();
            }
        }

        private void UpdateOriginalTextData()
        {
            if (_pages != null && _currentPageIndex >= 0 && _currentPageIndex < _pages.Count)
            {
                _pages[_currentPageIndex].ExtractedText = _rtbOriginal.Text;
            }
            UpdateStats();
        }

        private void UpdateStats()
        {
            string txt = _rtbOriginal.Text;
            int chars = txt.Length;
            int words = string.IsNullOrWhiteSpace(txt) ? 0 : txt.Split(new char[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            _statusStats.Text = string.Format("{0} palavras | {1} caracteres", words, chars);
        }

        private void TranslateCurrentPage()
        {
            if (_pages == null || _pages.Count == 0)
            {
                MessageBox.Show("Abra um documento PDF ou de texto antes de traduzir.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool visionMode = _cbMode != null && _cbMode.SelectedIndex == 1;
            string sourceText = _rtbOriginal.Text;
            if (!visionMode && string.IsNullOrWhiteSpace(sourceText))
            {
                MessageBox.Show("A página atual está vazia ou não contém texto extraível.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string imagePath = GetCurrentRenderedImagePath();
            if (visionMode && (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)))
            {
                MessageBox.Show("A imagem da página ainda não foi renderizada. Aguarde a visualização terminar e tente novamente.", "OCR Vision", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RenderPdfPreview();
                return;
            }

            string model = _cbModel.SelectedItem != null ? _cbModel.SelectedItem.ToString() : "qwen2.5:7b";
            string targetLang = _cbLang.SelectedItem != null ? _cbLang.SelectedItem.ToString() : "Português (BR)";

            string systemPrompt = string.Format(
                "You are an expert technical translator. Translate the given document faithfully into {0}. " +
                "IMPORTANT: Use Markdown for structure (headers, lists, bold, etc.) and LaTeX for all mathematical formulas (wrap formulas in $ for inline and $$ for blocks). " +
                "Preserve all technical terminology and document structure. " +
                "Do NOT add conversational meta-talk or introductory greetings. Return ONLY the translated document.",
                targetLang
            );

            string prompt = visionMode
                ? string.Format("Read the attached document page image with OCR. Reconstruct its text faithfully, fix OCR errors using visual context, and translate it into {0}. Return ONLY the corrected translation in Markdown.", targetLang)
                : string.Format("Correct extraction artifacts, preserve the document structure, and translate the following raw text into {0}:\n\n{1}", targetLang, sourceText);

            List<string> images = null;
            if (visionMode)
            {
                images = new List<string> { Convert.ToBase64String(File.ReadAllBytes(imagePath)) };
            }

            SetTranslatingState(true);
            _translationRenderTimer.Stop();
            _currentTranslation = "";
            _wbTranslated.DocumentText = GetTranslatingStateHtml();
            _statusLabel.Text = string.Format("Traduzindo página {0} com modelo '{1}'...", _currentPageIndex + 1, model);

            _cts = new CancellationTokenSource();
            var sbAccumulator = new StringBuilder();

            _ollama.StreamGenerate(
                model,
                prompt,
                systemPrompt,
                chunk =>
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        sbAccumulator.Append(chunk);
                        _currentTranslation = sbAccumulator.ToString();
                        // Limit HTML reloads while preserving incremental display.
                        if (!_translationRenderTimer.Enabled) _translationRenderTimer.Start();
                    }));
                },
                () =>
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        SetTranslatingState(false);
                        _translationRenderTimer.Stop();
                        string fullTranslation = sbAccumulator.ToString();
                        if (!string.IsNullOrWhiteSpace(fullTranslation))
                        {
                            _currentTranslation = fullTranslation;
                            RenderTranslation(fullTranslation);
                            _cache.SetTranslation(_currentFilePath, _currentPageIndex + 1, targetLang, GetCacheModelKey(), fullTranslation);
                        }
                        else
                        {
                            _wbTranslated.DocumentText = GetEmptyStateHtml();
                            _statusLabel.Text = "O modelo não retornou texto para esta página.";
                            _isTranslatingAll = false;
                            _progressBar.Visible = false;
                            return;
                        }
                        _statusLabel.Text = string.Format("Tradução da página {0} concluída com sucesso.", _currentPageIndex + 1);

                        if (_isTranslatingAll)
                        {
                            ContinueTranslateAll();
                        }
                    }));
                },
                ex =>
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        SetTranslatingState(false);
                        _translationRenderTimer.Stop();
                        _isTranslatingAll = false;
                        _progressBar.Visible = false;
                        _statusLabel.Text = "Erro na tradução: " + ex.Message;
                        MessageBox.Show("Erro de comunicação com o Ollama: " + ex.Message + "\n\nVerifique se o Ollama está rodando.", "Erro Ollama", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                },
                _cts,
                images
            );
        }

        private string GetCacheModelKey()
        {
            string model = _cbModel != null && _cbModel.SelectedItem != null ? _cbModel.SelectedItem.ToString() : "default";
            string mode = _cbMode != null && _cbMode.SelectedIndex == 1 ? "vision" : "text";
            return model + "::" + mode;
        }

        private string GetCurrentRenderedImagePath()
        {
            if (string.IsNullOrEmpty(_currentFilePath) ||
                !string.Equals(Path.GetExtension(_currentFilePath), ".pdf", StringComparison.OrdinalIgnoreCase)) return null;
            return Path.Combine(Path.GetTempPath(), "TradutorPdfOllama", "page-" + (_currentPageIndex + 1) + ".png");
        }

        private void TranslateAllPages()
        {
            if (_pages == null || _pages.Count == 0)
            {
                MessageBox.Show("Abra um documento antes de traduzir.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                string.Format("Deseja traduzir todas as {0} página(s) sequencialmente?", _pages.Count),
                "Confirmar Tradução em Lote",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (confirm != DialogResult.Yes) return;

            _isTranslatingAll = true;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = _pages.Count;
            _progressBar.Value = 0;
            _progressBar.Visible = true;

            _currentPageIndex = 0;
            UpdatePageView();
            TranslateCurrentPage();
        }

        private void ContinueTranslateAll()
        {
            if (!_isTranslatingAll) return;

            _progressBar.Value = Math.Min(_progressBar.Maximum, _currentPageIndex + 1);

            if (_currentPageIndex < _pages.Count - 1)
            {
                _currentPageIndex++;
                UpdatePageView();
                TranslateCurrentPage();
            }
            else
            {
                _isTranslatingAll = false;
                _progressBar.Visible = false;
                _statusLabel.Text = "Tradução de todas as páginas concluída!";
                MessageBox.Show("Tradução de todas as páginas finalizada com sucesso!", "Concluído", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void CancelTranslation()
        {
            _translationRenderTimer.Stop();
            if (_cts != null)
            {
                _cts.Cancel();
            }
            _isTranslatingAll = false;
            _progressBar.Visible = false;
            SetTranslatingState(false);
            _statusLabel.Text = "Tradução cancelada pelo usuário.";
        }

        private void SetTranslatingState(bool translating)
        {
            _btnTranslatePage.Enabled = !translating;
            _btnTranslateAll.Enabled = !translating;
            _btnStop.Enabled = translating;
            _btnOpen.Enabled = !translating;
        }

        private void ExportMarkdown()
        {
            if (_pages == null || _pages.Count == 0)
            {
                MessageBox.Show("Nenhum documento carregado para exportar.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                string baseName = Path.GetFileNameWithoutExtension(_currentFilePath ?? "traducao");
                sfd.FileName = baseName + "_traduzido.md";
                sfd.Filter = "Markdown (*.md)|*.md|Texto (*.txt)|*.txt|Todos os Arquivos (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(string.Format("# {0} (Tradução)", baseName));
                    sb.AppendLine();

                    string model = GetCacheModelKey();
                    string lang = _cbLang.SelectedItem != null ? _cbLang.SelectedItem.ToString() : "Português (BR)";

                    for (int i = 0; i < _pages.Count; i++)
                    {
                        sb.AppendLine(string.Format("## --- Página {0} ---", i + 1));
                        sb.AppendLine();
                        string trans = _cache.GetTranslation(_currentFilePath, i + 1, lang, model);
                        if (string.IsNullOrEmpty(trans) && i == _currentPageIndex)
                        {
                            trans = _currentTranslation;
                        }
                        if (string.IsNullOrEmpty(trans))
                        {
                            trans = "[Página não traduzida]";
                        }
                        sb.AppendLine(trans);
                        sb.AppendLine();
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    _statusLabel.Text = "Exportado com sucesso para " + Path.GetFileName(sfd.FileName);
                    MessageBox.Show("Documento exportado com sucesso!", "Exportação", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void CopyTranslation()
        {
            // We need to extract text from the WebBrowser's body
            string text = "";
            if (_wbTranslated.Document != null)
            {
                text = _wbTranslated.Document.Body.InnerText;
            }

            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
                _statusLabel.Text = "Tradução copiada para a área de transferência!";
            }
        }

        private string GetEmptyStateHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
<meta charset='UTF-8'>
<style>
  body {
    background-color: #FFFFFF;
    color: #475569;
    font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, 'Segoe UI Variable Text', sans-serif;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    min-height: 85vh;
    margin: 0;
    text-align: center;
  }
  .card {
    padding: 36px 30px;
    border: 1px dashed #CBD5E1;
    border-radius: 16px;
    background-color: #F8FAFC;
    max-width: 440px;
    box-shadow: 0 4px 16px rgba(15, 23, 42, 0.04);
  }
  .feather-icon {
    font-size: 38px;
    line-height: 1;
    margin-bottom: 14px;
  }
  h3 {
    margin: 0 0 8px 0;
    color: #0F172A;
    font-size: 17px;
    font-weight: 700;
  }
  p {
    margin: 0;
    font-size: 13px;
    color: #64748B;
    line-height: 1.6;
  }
  .slogan-pill {
    display: inline-block;
    background-color: #E0F2FE;
    color: #0284C7;
    padding: 5px 14px;
    border-radius: 9999px;
    font-size: 11.5px;
    font-weight: 600;
    margin-top: 16px;
    border: 1px solid #BAE6FD;
  }
</style>
</head>
<body>
  <div class='card'>
    <div class='feather-icon'>🪶</div>
    <h3>Mimai PDF</h3>
    <p>Abra um documento e clique em <b>▶ Traduzir Página</b> para traduzir em tempo real com seu modelo Ollama local.</p>
    <div class='slogan-pill'>mim não traduz, mim faz tradução</div>
  </div>
</body>
</html>";
        }

        private string GetTranslatingStateHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
<meta charset='UTF-8'>
<style>
  body {
    background-color: #FFFFFF;
    color: #0284C7;
    font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, 'Segoe UI Variable Text', sans-serif;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    min-height: 85vh;
    margin: 0;
    text-align: center;
  }
  .feather {
    font-size: 40px;
    margin-bottom: 14px;
  }
  .msg {
    font-size: 16px;
    font-weight: 700;
    color: #0284C7;
    margin-bottom: 6px;
  }
  .sub {
    font-size: 12.5px;
    color: #64748B;
  }
</style>
</head>
<body>
  <div class='feather'>🪶</div>
  <div class='msg'>Mimai fazendo tradução...</div>
  <div class='sub'>Processando tokens em tempo real com Ollama local</div>
</body>
</html>";
        }

        internal static WebBrowser CreateTranslationViewer()
        {
            var viewer = new WebBrowser();
            // DocumentText navigates to about:blank on every replacement. Disabling
            // all navigation freezes the viewer after its first document.
            viewer.AllowNavigation = true;
            viewer.ScriptErrorsSuppressed = true;
            viewer.Navigating += (s, e) =>
            {
                e.Cancel = e.Url == null || !string.Equals(
                    e.Url.AbsoluteUri, "about:blank", StringComparison.OrdinalIgnoreCase);
            };
            viewer.NewWindow += (s, e) => e.Cancel = true;
            return viewer;
        }

        private void RenderTranslation(string markdown)
        {
            string htmlContent = ConvertMarkdownToHtml(markdown);

            string fullHtml = string.Format(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{
            background-color: #FFFFFF;
            color: #0F172A;
            font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, 'Segoe UI Variable Text', Roboto, sans-serif;
            padding: 24px;
            line-height: 1.7;
            font-size: 14px;
        }}
        h1 {{
            color: #0284C7;
            border-bottom: 2px solid #E0F2FE;
            padding-bottom: 8px;
            font-size: 21px;
            font-weight: 700;
            margin-top: 0;
        }}
        h2 {{
            color: #0369A1;
            margin-top: 22px;
            font-size: 17px;
            font-weight: 600;
            border-bottom: 1px solid #F1F5F9;
            padding-bottom: 6px;
        }}
        h3 {{
            color: #0EA5E9;
            font-size: 15px;
            font-weight: 600;
            margin-top: 16px;
        }}
        p {{
            margin-bottom: 12px;
            color: #1E293B;
        }}
        b, strong {{
            color: #0F172A;
            font-weight: 700;
        }}
        i, em {{
            color: #0284C7;
            font-style: italic;
        }}
        code {{
            background-color: #F1F5F9;
            color: #0284C7;
            padding: 2px 6px;
            border-radius: 5px;
            font-family: 'Consolas', 'Cascadia Code', monospace;
            font-size: 13px;
            border: 1px solid #E2E8F0;
        }}
        pre {{
            background-color: #F8FAFC;
            color: #0F172A;
            padding: 16px;
            border-radius: 8px;
            overflow-x: auto;
            font-family: 'Consolas', 'Cascadia Code', monospace;
            font-size: 13px;
            border: 1px solid #E2E8F0;
            line-height: 1.5;
        }}
        ul, ol {{
            margin-left: 22px;
            margin-bottom: 12px;
            color: #1E293B;
        }}
        li {{
            margin-bottom: 5px;
        }}
        blockquote {{
            border-left: 4px solid #0284C7;
            background-color: #F0F9FF;
            margin: 14px 0;
            padding: 10px 16px;
            color: #0369A1;
            border-radius: 0 8px 8px 0;
            font-style: italic;
        }}
        .mjx-chtml {{ color: #0F172A !important; }}
        .math {{ font-family: 'Cambria Math', 'Consolas', monospace; color: #334155; white-space: pre-wrap; }}
    </style>
</head>
<body>
    {0}
</body>
</html>", htmlContent);

            _wbTranslated.DocumentText = fullHtml;
        }

        private string ConvertMarkdownToHtml(string md)
        {
            if (string.IsNullOrEmpty(md)) return "";

            string result = md;

            // Preserve LaTeX while applying the small Markdown converter.
            // Otherwise '*' and '_' inside expressions are interpreted as
            // Markdown emphasis and corrupt the formula.
            var mathBlocks = new List<string>();
            result = System.Text.RegularExpressions.Regex.Replace(result,
                @"(\$\$[\s\S]*?\$\$|\$[^$\r\n]+\$|\\\[[\s\S]*?\\\]|\\\([\s\S]*?\\\))",
                new System.Text.RegularExpressions.MatchEvaluator(match =>
                {
                    string value = match.Value;
                    int index = mathBlocks.Count;
                    mathBlocks.Add(value);
                    return "@@MATH" + index + "@@";
                }));

            // Escaping basic HTML to prevent injection but keeping our converted tags
            result = result.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

            // Bold
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\*\*(.*?)\*\*", "<b>$1</b>");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"__(.*?)__", "<b>$1</b>");

            // Italic
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\*(.*?)\*", "<i>$1</i>");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"_(.*?)_", "<i>$1</i>");

            // Headers
            result = System.Text.RegularExpressions.Regex.Replace(result, @"^# (.*)$", "<h1>$1</h1>", System.Text.RegularExpressions.RegexOptions.Multiline);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"^## (.*)$", "<h2>$1</h2>", System.Text.RegularExpressions.RegexOptions.Multiline);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"^### (.*)$", "<h3>$1</h3>", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Code blocks (simple)
            result = System.Text.RegularExpressions.Regex.Replace(result, @"^`{3}(.*?)\n(.*?)\n`{3}$", "<pre>$2</pre>", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"`(.*?)`", "<code>$1</code>");

            // Lists (simple)
            result = System.Text.RegularExpressions.Regex.Replace(result, @"^\* (.*)$", "<li>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"^- (.*)$", "<li>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Wrap lists in <ul>
            // This is a simplified approach; a real parser would be better, but this works for basic needs
            result = System.Text.RegularExpressions.Regex.Replace(result, @"(<li>.*</li>)+", "<ul>$0</ul>", System.Text.RegularExpressions.RegexOptions.Singleline);

            // Paragraphs (double newline)
            result = result.Replace("\n\n", "<br/><br/>").Replace("\n", "<br/>");

            for (int i = 0; i < mathBlocks.Count; i++)
            {
                string math = mathBlocks[i].Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                result = result.Replace("@@MATH" + i + "@@", "<span class='math'>" + math + "</span>");
            }

            return result;
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                LoadFile(files[0]);
            }
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.O)
            {
                OpenFileDialogHandler();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.T)
            {
                TranslateCurrentPage();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                ExportMarkdown();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Left)
            {
                NavigatePage(-1);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Right)
            {
                NavigatePage(1);
                e.Handled = true;
            }
        }
    }

    public class ModernPantoneColorTable : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin { get { return Color.FromArgb(255, 255, 255); } }
        public override Color ToolStripGradientMiddle { get { return Color.FromArgb(255, 255, 255); } }
        public override Color ToolStripGradientEnd { get { return Color.FromArgb(255, 255, 255); } }
        public override Color ToolStripBorder { get { return Color.FromArgb(226, 232, 240); } }

        public override Color ButtonSelectedHighlight { get { return Color.FromArgb(240, 249, 255); } }
        public override Color ButtonSelectedGradientBegin { get { return Color.FromArgb(240, 249, 255); } }
        public override Color ButtonSelectedGradientMiddle { get { return Color.FromArgb(240, 249, 255); } }
        public override Color ButtonSelectedGradientEnd { get { return Color.FromArgb(240, 249, 255); } }
        public override Color ButtonSelectedBorder { get { return Color.FromArgb(56, 189, 248); } }

        public override Color ButtonPressedHighlight { get { return Color.FromArgb(224, 242, 254); } }
        public override Color ButtonPressedGradientBegin { get { return Color.FromArgb(224, 242, 254); } }
        public override Color ButtonPressedGradientMiddle { get { return Color.FromArgb(224, 242, 254); } }
        public override Color ButtonPressedGradientEnd { get { return Color.FromArgb(224, 242, 254); } }
        public override Color ButtonPressedBorder { get { return Color.FromArgb(2, 132, 199); } }

        public override Color ButtonCheckedGradientBegin { get { return Color.FromArgb(224, 242, 254); } }
        public override Color ButtonCheckedGradientMiddle { get { return Color.FromArgb(224, 242, 254); } }
        public override Color ButtonCheckedGradientEnd { get { return Color.FromArgb(224, 242, 254); } }
        public override Color ButtonCheckedHighlight { get { return Color.FromArgb(224, 242, 254); } }

        public override Color SeparatorDark { get { return Color.FromArgb(226, 232, 240); } }
        public override Color SeparatorLight { get { return Color.FromArgb(255, 255, 255); } }

        public override Color StatusStripGradientBegin { get { return Color.FromArgb(255, 255, 255); } }
        public override Color StatusStripGradientEnd { get { return Color.FromArgb(255, 255, 255); } }
    }
}
