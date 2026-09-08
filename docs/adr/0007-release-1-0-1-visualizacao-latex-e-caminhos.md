# ADR-0007 — Release 1.0.1: visualização LaTeX e caminhos robustos

## Status

Aceito

## Data

2026-09-08

## Contexto

Os scripts `.bat` dependiam implicitamente do diretório atual e podiam falhar
quando o projeto estivesse em um caminho com espaços. A visualização de
traduções também precisava preservar expressões LaTeX sem corrompê-las durante
a conversão Markdown.

## Decisão

Publicar a correção como `v1.0.1`, incluindo:

- resolução de caminhos baseada na localização dos próprios scripts;
- suporte a caminhos com espaços e caracteres especiais;
- normalização literal dos caminhos de entrada e saída do renderizador PDF;
- preservação das expressões LaTeX na visualização local.

## Consequências

A execução por `build.bat` e `run.bat` torna-se independente do diretório a
partir do qual o comando foi iniciado. A versão `v1.0.1` documenta e distribui
a correção da visualização LaTeX junto com os ajustes de caminhos.
