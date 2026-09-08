# ADR 0002: Descontinuação e Remoção do Protótipo Web Paralelo

- **Status**: Aceito
- **Data**: 2026-09-08
- **Decisores**: Equipe Mimai PDF

## Contexto
O repositório mantinha arquivos residuais de um protótipo web inicial (index.html, pp.js, package.json). O aplicativo nativo de produção em C# WinForms não consumia nem referenciava esses arquivos, gerando confusão de manutenção e ambiguidade arquitetural.

## Decisão
Remover completamente o aplicativo web paralelo (index.html, pp.js e package.json), consolidando o repositório como uma solução desktop nativa pura.

## Consequências
- Código mais limpo e focado.
- Eliminação de falsas dependências com ecossistema npm/node.
- Manutenção simplificada centralizada em src/, 	ools/ e scripts de automação.
