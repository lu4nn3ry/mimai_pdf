# ADR-0009 — Chat contextual e edição da tradução

## Status

Aceito

## Data

2026-09-15

## Contexto

O leitor precisa esclarecer dúvidas sobre a página atual e solicitar correções
sem sair do documento. A interface deve distinguir uma pergunta de uma alteração
e impedir que uma resposta atrasada modifique outra página.

## Decisão

- Adicionar uma terceira coluna redimensionável com o chat, mantendo original
  e tradução visíveis. Usar o modelo selecionado na barra e o Ollama configurado
  localmente pelo aplicativo.
- Exibir respostas no visualizador HTML local com o mesmo conversor Markdown
  e renderizador matemático da tradução, agrupando atualizações a cada 250 ms
  e renderizando o conteúdo final ao concluir. Preservar código literal e
  escapar HTML recebido, mantendo o bloqueio de navegação externa.
- Enviar original, tradução atual e até seis pares recentes da conversa como
  contexto. O histórico é mantido em memória e reiniciado ao mudar página,
  documento, idioma ou modelo/modo de tradução.
- Oferecer modos explícitos: perguntas apenas respondem; “Editar a tradução”
  solicita ao modelo o Markdown completo revisado. A edição atualiza o visor e
  a mesma entrada de cache usada na navegação/exportação.
- Guardar a versão anterior para desfazer a última edição. Uma alteração externa
  da tradução invalida esse desfazer.
- Cancelar solicitações pendentes quando o contexto muda, ao iniciar tradução
  ou ao fechar o painel. Conferir a tradução capturada antes de aplicar a resposta.
- Exigir o marcador final do streaming Ollama. Erros e respostas interrompidas
  não aplicam edições parciais; cancelamento interrompe a requisição HTTP.

## Consequências

O chat usa a página atual, não o documento inteiro. Perguntas exibem resposta
incremental; edições aparecem apenas após conclusão. Conversas não são persistidas
em disco. Modelos podem produzir revisões inadequadas, recuperáveis por “Desfazer”.
Testes com cliente simulado verificam contexto, perguntas sem alterações, edição,
desfazer, cancelamento, respostas atrasadas e layout na largura mínima.
