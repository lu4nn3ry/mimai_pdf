# ADR-0006 — Visualizador local sem JavaScript remoto

## Status

Aceito

## Data

2026-09-08

## Contexto

O painel de tradução usa o controle `WebBrowser` do WinForms. O carregamento
do MathJax a partir de uma CDN externa fazia o mecanismo legado do navegador
solicitar permissão para executar JavaScript. Além disso, o conversor Markdown
interpretava `*` e `_` presentes em fórmulas LaTeX como formatação Markdown,
corrompendo expressões matemáticas exibidas.

O visualizador de páginas PDF também precisava informar falhas de renderização
de forma determinística quando o processo externo excedesse o tempo limite ou
terminasse com erro.

## Decisão

- Remover o carregamento de MathJax e manter o painel limitado a HTML local.
- Desabilitar navegação e suprimir diálogos de script do `WebBrowser`.
- Preservar blocos LaTeX antes da conversão Markdown e exibi-los com estilo
  monoespaçado quando não houver um renderizador matemático local.
- Encerrar o processo de renderização após 60 segundos e apresentar o erro ou
  timeout no status da aplicação.

## Consequências

O aplicativo deixa de solicitar permissão para JavaScript externo e evita
dependência de rede no visualizador. Fórmulas deixam de ser alteradas pelo
parser Markdown, embora sejam exibidas como LaTeX textual em vez de receberem
renderização tipográfica do MathJax. Falhas do renderizador passam a ser
diagnosticáveis pela mensagem de status.
