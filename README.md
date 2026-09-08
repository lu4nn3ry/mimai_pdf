<p align="center">
  <img src="banner.png" alt="Mimai PDF" width="100%">
</p>

<h1 align="center">Mimai PDF</h1>

<p align="center"><em>“mim não traduz, mim faz tradução”</em></p>

Aplicação desktop nativa para Windows 11 que abre, visualiza, reconhece e traduz documentos localmente usando C#, WinForms, APIs nativas do Windows e Ollama.

## Escopo atual

- Leitura de PDF, TXT e Markdown.
- Extração de texto por página com parser C# embutido.
- Visualização real de PDF com `Windows.Data.Pdf`.
- OCR local para imagens e páginas escaneadas com `Windows.Media.Ocr`.
- Dois modos de tradução: texto extraído corrigido pelo modelo ou OCR por visão enviando a imagem da página ao modelo.
- Tradução por streaming através do Ollama local.
- Cache persistente por documento, página, idioma e modelo.
- Navegação página a página e tradução em lote.
- Exportação em Markdown e cópia da tradução.

O projeto é Windows-only e não depende de Node.js, Python, Rust, Electron ou Tauri. A compilação usa o `csc.exe` do .NET Framework instalado no Windows.

## Arquitetura

```text
Program.cs
    └── MainForm.cs                 interface e coordenação do fluxo
        ├── PdfExtractor.cs         extração de texto e páginas
        ├── render_pdf_page.ps1     renderização via Windows.Data.Pdf
        ├── ocr_windows.ps1         OCR via Windows.Media.Ocr
        ├── OllamaClient.cs         HTTP e streaming em /api/generate
        └── TranslationCache.cs     cache em %APPDATA%
```

O formulário coordena o fluxo atual. A separação dos componentes deixa a extração, renderização, OCR, tradução e persistência substituíveis sem alterar a janela inteira. As decisões arquiteturais estão registradas em [`docs/adr/`](docs/adr/README.md).

## Estrutura

```text
src/
  Program.cs
  MainForm.cs
  PdfExtractor.cs
  OllamaClient.cs
  TranslationCache.cs
tools/
  render_pdf_page.ps1
  ocr_windows.ps1
docs/adr/
tests/
build.bat
build.ps1
run.bat
mimai_pdf.exe
```

## Requisitos

- Windows 10/11 com suporte às APIs `Windows.Data.Pdf` e `Windows.Media.Ocr`.
- .NET Framework 4.x com `csc.exe` para compilar.
- Ollama instalado e em execução em `http://localhost:11434` para traduzir.
- Um modelo Ollama, por exemplo `qwen2.5:7b`.

## Compilar e executar

Pelo terminal:

```powershell
.\build.ps1
.\mimai_pdf.exe
```

Ou execute `build.bat` e depois `run.bat`. O executável gerado é `mimai_pdf.exe`.

Para preparar o Ollama:

```powershell
ollama run qwen2.5:7b
```

Depois abra um PDF pelo botão, arraste o arquivo para a janela ou use `Ctrl+O`.

## Fluxo de processamento

1. O documento é carregado pelo `PdfExtractor`.
2. O texto é associado às páginas extraídas.
3. A página atual é renderizada como imagem pelo `render_pdf_page.ps1`.
4. O usuário escolhe entre “Texto extraído (manual)” ou “OCR com IA Vision”.
5. No modo manual, o texto bruto é corrigido e traduzido; no modo Vision, a imagem renderizada é enviada ao modelo.
6. O resultado é recebido do Ollama em streaming.
7. A tradução é exibida e salva no cache.
8. O documento traduzido pode ser exportado para Markdown.

## Limitações conhecidas

- O parser embutido usa heurísticas para reconstruir texto e pode perder ordem em PDFs com múltiplas colunas, tabelas ou fontes incomuns.
- O cache atual usa o nome do arquivo como parte da chave; alterações no conteúdo mantendo o mesmo nome podem exigir limpeza manual do cache.
- O OCR é acionado manualmente pela interface e ainda não é automático para toda página sem texto.
- O `MainForm` ainda concentra parte da coordenação do fluxo; a evolução prevista está em [`TODO.md`](TODO.md).

## ADRs

- [0001 — Arquitetura nativa Windows/C#](docs/adr/0001-arquitetura-nativa-windows-csharp.md)
- [0002 — Remoção do protótipo web](docs/adr/0002-remocao-prototipo-web.md)
- [0003 — Identidade visual Mimai PDF](docs/adr/0003-identidade-visual-mimai-pdf.md)
- [0004 — Modernização da UX/UI](docs/adr/0004-modernizacao-ux-ui-pantone-2026.md)

## Licença

MIT.
