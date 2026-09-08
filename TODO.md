# TODO — Mimai PDF

Este arquivo registra a evolução planejada dentro do escopo do Mimai PDF. A arquitetura base está fechada: aplicação desktop nativa Windows, C# WinForms, renderização por `Windows.Data.Pdf`, OCR por `Windows.Media.Ocr`, Ollama local e cache persistente.

## Prioridade alta

- [ ] Criar `DocumentService` para tirar abertura, navegação e estado de documento do `MainForm`.
- [ ] Criar `TranslationWorkflow` para coordenar tradução de uma página e tradução em lote.
- [ ] Criar `PdfRenderer` para encapsular a chamada ao `render_pdf_page.ps1`, incluindo timeout, cancelamento e mensagens de erro.
- [ ] Acionar OCR automaticamente quando a extração retornar vazia ou abaixo de um limite mínimo de texto.
- [ ] Trocar a chave do cache baseada apenas no nome do arquivo por hash do PDF ou do texto da página.
- [ ] Detectar automaticamente se o modelo selecionado suporta entrada de imagem antes de ativar o modo Vision.

## Prioridade média

- [ ] Adicionar testes para PDFs de uma coluna, duas colunas, tabelas, fontes incomuns e páginas escaneadas.
- [ ] Preservar melhor a ordem visual do texto usando posição, tamanho e orientação dos blocos extraídos.
- [ ] Separar o modelo de tradução da apresentação para permitir exportação TXT, Markdown e PDF sem depender do controle visual.
- [ ] Adicionar limpeza e migração de versões do cache.
- [ ] Melhorar cancelamento e retomada da tradução em lote.

## Prioridade baixa

- [ ] Adicionar zoom, ajuste à largura e rotação na visualização PDF.
- [ ] Adicionar seleção e cópia de texto diretamente na página renderizada.
- [ ] Permitir configurar endereço do Ollama, timeout e prompt de tradução.
- [ ] Criar pacote de distribuição com `mimai_pdf.exe`, scripts e recursos visuais.
- [ ] Adicionar telemetria local opcional para diagnóstico, sem enviar dados do documento.

## Fora do escopo atual

- [x] Aplicação web paralela.
- [x] Electron, Tauri ou runtime Python.
- [x] Serviço de tradução em nuvem obrigatório.
- [x] Suporte oficial a macOS ou Linux.
