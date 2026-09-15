# ADR-0008 — Atualização do visor e renderização matemática local

## Status

Aceito. Substitui parcialmente o ADR-0006 quanto ao bloqueio de navegação
e à apresentação de LaTeX como texto.

## Data

2026-09-15

## Contexto

O aplicativo confirmava traduções concluídas, mas o visor mantinha o primeiro
HTML. Um teste com o WebBrowser reproduziu que `AllowNavigation = false`
bloqueia as navegações para `about:blank` usadas por `DocumentText` para
substituir o documento. Após corrigir essa atualização, a captura do usuário
confirmou outro limite: fórmulas como `$q \le k/2$` ainda apareciam como código.

## Decisão

- Permitir apenas a navegação interna para `about:blank`, bloqueando endereços
  externos e novas janelas. Limitar atualizações do streaming a intervalos de
  250 ms e renderizar explicitamente o resultado final; resposta vazia não é sucesso.
- Renderizar fórmulas com WpfMath 0.11.0, versão compatível com .NET Framework
  4.5.2 e sem dependências NuGet adicionais. Incorporar DLL e licença MIT ao
  executável e carregar a biblioteca em memória.
- Gerar PNGs locais para `$...$`, `$$...$$`, `\(...\)` e `\[...\]`, com fórmulas
  de bloco centralizadas. Usar uma pasta temporária exclusiva por processo,
  reutilizar resultados e apagar as imagens ao encerrar normalmente.
- Proteger código e fórmulas antes da conversão Markdown. Fórmulas inválidas,
  incompletas ou não suportadas permanecem como LaTeX escapado, sem executar código.
- Preservar o Markdown/LaTeX original no cache, na cópia e na exportação.

## Consequências e validação

O visor funciona sem JavaScript remoto e sem rede para tipografia matemática.
O executável aumenta aproximadamente 360 KB e passa a usar os assemblies WPF
fornecidos pelo .NET Framework. A biblioteca não implementa todo o LaTeX;
comandos não suportados usam o fallback textual. Uma interrupção abrupta pode
deixar imagens temporárias até a limpeza da pasta temporária do Windows.

`tests/run.ps1` testa atualizações sucessivas do HTML, rajadas de streaming,
troca de conteúdo de páginas, bloqueio de navegação externa, fórmulas da captura,
frações, gregas, somatórios, quatro delimitadores, código literal e fallback escapado.
Os testes carregam a DLL incorporada, sem depender de uma instalação do WpfMath.
