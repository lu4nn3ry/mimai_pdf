# ADR 0005: Aceitar caminhos de PDF com colchetes e caracteres especiais

- **Status**: Aceito
- **Data**: 2026-09-08
- **Decisão**: O renderizador deve validar caminhos de arquivos com `Test-Path -LiteralPath`.

## Contexto

Artigos matemáticos frequentemente têm nomes como `arXiv... [math.CO] ...pdf`. O PowerShell interpreta colchetes como curingas quando um caminho é validado com `Test-Path` sem `-LiteralPath`, fazendo o aplicativo informar que um PDF existente não foi encontrado.

## Consequência

O caminho recebido pelo renderizador passa a ser tratado literalmente. Isso mantém suporte a colchetes, espaços, acentos e outros caracteres válidos em nomes de arquivos Windows.
