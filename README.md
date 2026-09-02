# Tradutor de PDF & OCR com IA Local (Ollama)

Tradutor de PDFs em tempo real com preservação de fluxo, visualização lado a lado e aceleração por IA Local (Ollama) e OCR.

## ✨ Recursos

- ⚡ **Streaming em Tempo Real**: Tradução token a token diretamente do Ollama (`qwen2.5`, `llama3.2`, `deepseek-r1`, etc.).
- 📖 **Split-View Bilíngue**: Visualizador do PDF original na esquerda via PDF.js e texto traduzido na direita com suporte a Markdown.
- 🔀 **Multi-Idiomas**: Português (BR), Inglês, Espanhol, Francês, Alemão e outros.
- 💾 **Exportação**: Baixe o documento traduzido em Markdown estruturado página a página.
- 🔌 **Ecossistema Completo**:
  - **Standalone Web**: `index.html` + `app.js` (executável em qualquer navegador).
  - **Harness CLI**: Comando `chronokairo translate <arquivo.pdf>` no terminal Rust.
  - **Harness Desktop**: Aba integrada no `chronokairo-desktop` (Tauri + React).

## 🚀 Como Executar

### 1. Iniciar o Ollama
Certifique-se de que o Ollama está rodando localmente com o modelo desejado baixado:
```bash
ollama run qwen2.5:7b
```

### 2. Abrir o Web Reader
Basta abrir o arquivo `index.html` em qualquer navegador ou rodar um servidor web simples:
```bash
npx serve .
# ou
python -m http.server 8080
```