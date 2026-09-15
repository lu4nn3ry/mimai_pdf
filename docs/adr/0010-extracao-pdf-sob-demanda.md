# ADR-0010 — Extração PDF sob demanda por página

## Status

Aceito

## Data

2026-09-15

## Contexto

A abertura do PDF extraía todos os fluxos de conteúdo e distribuía o texto
entre páginas por estimativa. Isso processava páginas ainda não solicitadas,
inclusive quando o usuário pretendia usar o modo Vision, e podia misturar páginas.

## Decisão

- Abrir o PDF com PdfPig 0.1.9 para obter sua estrutura e contagem de páginas.
  Criar registros de páginas sem texto; não chamar `GetPage` nessa etapa.
- Extrair texto somente ao traduzir a página selecionada no modo Texto extraído,
  em uma tarefa de fundo. Usar a ordem real da árvore de páginas do PDF, com
  `ContentOrderTextExtractor` para o conteúdo da página solicitada.
- Reutilizar o texto já obtido em memória e preservar alterações manuais.
  Distinguir página ainda não extraída de página extraída que não contém texto.
- No modo Vision, enviar apenas a imagem da página atual. Não executar o
  extrator textual. Aguardar a imagem quando necessário, inclusive no lote.
- Reutilizar imagens renderizadas da sessão e separar sua pasta por documento
  aberto. Uma resposta de renderização antiga não pode substituir a página atual.
- Cancelamento ou mudança de contexto impede que uma extração pendente dispare
  uma tradução posterior. O lote prepara e traduz cada página sequencialmente.
- Incorporar PdfPig e dependências, licenças e avisos ao executável. O requisito
  mínimo passa a .NET Framework 4.7.1, sem instalação separada de pacotes.

## Consequências

Abrir/navegar PDFs ainda exige ler metadados e renderizar a imagem selecionada,
mas não extrai seu texto. A aba textual fica vazia até a tradução no modo texto
ou uma edição manual. O chat não provoca extração: usa apenas o texto disponível
e a tradução atual. PDFs escaneados exigem Vision. A leitura de PDFs complexos
ainda pode depender da estrutura e das fontes do arquivo.

O executável cresce devido à biblioteca PDF incorporada. Testes com PDF de
três páginas verificam ausência de extração na abertura, navegação e Vision,
ordem real de páginas, extração da página selecionada ao traduzir, preservação
de página vazia e reutilização do texto corrigido.
