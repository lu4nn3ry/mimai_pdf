# ADR-0011 — Suporte a leitura e tradução de documentos EPUB

## Status

Aceito

## Data

2026-09-17

## Contexto

O Mimai PDF atendia documentos em PDF, TXT e Markdown. Usuários também estudam artigos, livros e documentações técnicas no formato EPUB (.epub), que agrega múltiplos capítulos em XHTML/HTML com fórmulas matemáticas, estrutura semântica e capa dentro de um contêiner ZIP.

Converter previamente o EPUB para PDF ou texto puro degradava a experiência: a conversão para PDF gerava arquivos pesados e a conversão para TXT perdia capítulos, títulos, tabelas e anotações matemáticas. Era necessário ler e traduzir arquivos EPUB diretamente no Mimai PDF de forma nativa.

## Decisão

- Implementar `EpubExtractor` utilizando as APIs nativas de compressão do .NET Framework (`System.IO.Compression.ZipArchive`) e XML (`System.Xml.XmlDocument`), sem adicionar bibliotecas de terceiros ou runtimes externos.
- Analisar o `META-INF/container.xml` para localizar o arquivo do pacote (`.opf`), mapear itens do manifesto e processar a sequência linear de leitura definida pelo `<spine>`.
- Converter o conteúdo XHTML/HTML de cada capítulo em texto/Markdown estruturado, preservando títulos (`#`, `##`, etc.), parágrafos, listas, citações, tabelas e ênfases.
- Preservar fórmulas matemáticas em MathML por meio de anotações LaTeX (`<annotation encoding="application/x-tex">`), atributos `alttext`/`alt` ou spans matemáticos, convertendo-os para `$inline$` e `$$bloco$$` compatíveis com o renderizador local `WpfMath` e com o prompt do Ollama.
- Decodificar entidades HTML comuns e numéricas (`&quot;`, `&amp;`, `&lt;`, `&gt;`, `&nbsp;`, `&mdash;`, etc.) via `System.Net.WebUtility.HtmlDecode`.
- Paginar capítulos extensos automaticamente em blocos com quebras naturais de parágrafo, assegurando que o tamanho do texto enviado ao Ollama permaneça dentro dos limites da janela de contexto do modelo.
- Extrair a imagem de capa do livro (seja declarada por `properties="cover-image"`, metadado `cover` do EPUB 2 ou padrão de nome do arquivo) e exibi-la na aba lateral de visualização.
- Selecionar automaticamente a aba "Texto Extraído" e garantir modo textual ao abrir arquivos `.epub`, permitindo navegação capítulo a capítulo, cache persistente por página e tradução em lote.
- Adicionar referências a `System.IO.Compression.dll` e `System.IO.Compression.FileSystem.dll` nos scripts de compilação `build.ps1` e no executor de testes `tests/run.ps1`.

## Consequências

O Mimai PDF passa a abrir, navegar e traduzir livros e artigos em formato EPUB nativamente, com suporte total a tradução sequencial em lote, cache local em `%APPDATA%`, chat contextual na terceira coluna e renderização visual de equações matemáticas.

A compilação continua 100% nativa em Windows com `csc.exe`, sem dependência de gerenciadores de pacotes ou runtime adicional. A suíte de testes unitários agora cobre parsing, extração de MathML, decodificação de entidades, paginação de capítulos longos, extração de capa e mecanismos de fallback.
