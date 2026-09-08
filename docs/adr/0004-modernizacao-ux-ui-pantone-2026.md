# ADR 0004: Modernização de UX/UI com Paleta Pantone 2026 e Estilo Light Editorial

- **Status**: Aceito
- **Data**: 2026-09-08
- **Decisores**: Equipe Mimai PDF

## Contexto
A interface anterior utilizava um tema escuro genérico (*Catppuccin Mocha / Dark Navy*) que contrastava com a nova identidade visual do Mimai PDF e do ícone oficial da pena azul flutuante sobre fundo branco (*Pantone Pure White*). Era necessária uma experiência visual moderna, alinhada com as tendências de design de 2026 (minimalismo, alta legibilidade, contrastes refinados e sensação tátil editorial).

## Decisão
1. **Paleta de Cores Pantone 2026**:
   - **Base / Canvas**: Slate 50 (#F8FAFC / *Pantone Crisp Warm White*).
   - **Superfícies de Cards & Controles**: Pure White (#FFFFFF).
   - **Headers & Badges**: Slate 100 (#F1F5F9).
   - **Bordas & Separadores**: Slate 200 (#E2E8F0).
   - **Texto Primário**: Slate 900 (#0F172A) com alto contraste.
   - **Texto Secundário / Labels**: Slate 600 (#475569).
   - **Acento Principal**: *Pantone 2026 Celestial Azure* (#0284C7) inspirado na pena azul do ícone.
   - **Acento Secundário**: *Pantone Airy Cyan* (#38BDF8).

2. **Componentes Modernizados**:
   - **ToolStrip & StatusStrip**: Implementação de ModernPantoneColorTable com renderizador profissional plano (sem degradês obsoletos do Windows clássico).
   - **Navegação de Páginas**: Botões planos com bordas finas (1px solid #E2E8F0), cantos suaves e cursor hand.
   - **Renderizador Markdown / LaTeX**: Estilo editorial de leitura de revista científica (*clean light mode*), com citações em destaque azul Pantone, blocos de código com fundo suave #F8FAFC e fontes tipográficas de alta fidelidade (Segoe UI Variable Text).
   - **Empty State & Loading**: Telas de estado zero e carregamento estilizadas com a pena 🪶 e o slogan oficial *"mim não traduz, mim faz tradução"*.

## Consequências
- Visual limpo, profissional e contemporâneo para 2026.
- Consistência estética absoluta entre o ícone, o banner panorâmico e a interface interna do aplicativo.
- Redução da fadiga visual na leitura de textos longos de PDFs e traduções.
