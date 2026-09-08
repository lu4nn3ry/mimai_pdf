# ADR 0001: Adoção de Arquitetura 100% Nativa C# / WinForms sem Runtime Externo

- **Status**: Aceito
- **Data**: 2026-09-08
- **Decisores**: Equipe Mimai PDF

## Contexto
O projeto necessita de uma aplicação de desktop para Windows 11 capaz de extrair páginas de documentos PDF, invocar LLMs locais via Ollama por streaming e executar OCR local sem latência excessiva ou instalação pesada de dependências como Node.js, Python runtime ou Electron.

## Decisão
Adotar C# (.NET Framework 4.8 / WinForms) com compilação via csc.exe nativo do Windows:
1. Zero instalação para o usuário final: compila e executa direto no Windows 11.
2. Parser C# embutido com descompressão FlateDecode nativa (DeflateStream).
3. Renderização nativa de PDF via API Windows.Data.Pdf.
4. OCR nativo via integração direta com a API Windows.Media.Ocr.

## Consequências
- **Positivas**:
  - Binário único executável leve (~40 KB) sem dependências externas.
  - Inicialização instantânea e baixo consumo de memória RAM.
  - Integração perfeita com APIs modernas do Windows 11.
- **Negativas / Mitigações**:
  - Exclusivo para ambiente Windows (mitigado pelo foco estrito na plataforma desktop Windows 11).
