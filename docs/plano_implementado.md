# Plano Implementado

## Entregue

- Solucao Visual Studio `ColetorProfitRTD.sln`.
- Projeto C# .NET Framework 4.8 com configuracoes AnyCPU, x64 e x86.
- Cliente RTD em thread STA dedicada.
- Controle multiativo com cadastro, liga/desliga e assinatura dinamica por ativo.
- Tela principal `Ativos` com cadastro guiado de `price`, `book` e `timesTrades`.
- Persistencia em `data/assets/assets.json` e CSV por ativo em `data/assets/{ativo}/history.csv`.
- Exclusao de RTD por ativo e controle de fontes `price`, `book` e `timesTrades`.
- Catalogo RTD com os campos colados pelo usuario.
- Snapshot consolidado com `rtd`, `intraday` e `book`.
- Servidor local `HttpListener` com `/`, `/health`, `/snapshot`, `/flow`, `/signals`, `/assets`, `/assets/toggle`, `/assets/channels`, `/assets/delete`, `/assets/history` e `/ws`.
- WebSocket com broadcast de snapshots, status, flow, signals, `bookDepth` e `timesTrades`.
- SQLite auxiliar para snapshots de 1 segundo e consolidado por minuto.
- HTML do `dolar-points` importado e adaptado com menu superior agrupado em `Cadastro`, `Mercado`, `Fluxo` e `Controle`.
- Tela `Painel` como entrada operacional com checklist, atalhos, setups, planos e alertas.
- Tela `Monitor` como mesa ao vivo com watchlist compacta, estado do ativo, setups, tape, planos e alertas.
- Tela `Cotacoes` como watchlist operacional entre o cadastro de ativos e as telas de analise.
- Tela `Boleta` para planos locais de entrada, stop, alvo, risco e status por toque de preco, sem envio de ordens.
- Tela `Ajustes` para parametros locais de tick, DOM, renderizacao, memoria de tape/sinais e valor por ponto.
- Tela `Conexoes` para polling de `/health`, estado do coletor, Profit RTD, WebSocket e fontes por ativo.
- Faixa superior operacional com ativo selecionado, ultimo preco, bid/ask, Book, Times, delta e CSV.
- Telemetria WebSocket no frontend com latencia local, mensagens por segundo, reconexoes e contadores por tipo de mensagem.
- Coalescing auxiliar para `bookDepth` e `timesTrades`, reduzindo repintura sem bloquear snapshots de preco.
- Scheduler de render no navegador para agrupar DOM, Painel, Cotacoes e Historico, com padrao de 120 ms e ajuste pela UI, mantendo inputs de preco imediatos.

## Fluxo de validacao

1. Abrir Profit Pro.
2. Compilar x64 no Visual Studio.
3. Rodar o exe e abrir `http://localhost:5000`.
4. Se o RTD nao conectar, compilar x86.
5. Cadastrar ativo em `Ativos` e carregar CSV historico.
6. Confirmar que os campos intraday sao preenchidos por RTD.
7. Adicionar outro ativo em `Ativos`, testar `Ver`, `Ligado`, `Desligado`, fontes e `Excluir`.
8. Abrir `Painel` e confirmar checklist, atalhos, setups, planos e alertas.
9. Abrir `Monitor` e confirmar watchlist compacta, estado do ativo, setups, tape, planos e alertas.
10. Abrir `Cotacoes` e testar os atalhos para `Grafico`, `DOM`, `Book`, `T&T` e `Boleta`.
11. Salvar um plano local na `Boleta` e confirmar que nenhum endpoint de ordem e chamado.
12. Abrir `Book`, `T&T`, `Alertas`, `Risco`, `Historico`, `Ajustes`, `Conexoes` e `Sistema`.
13. Confirmar `Conexoes` com `/health`, arquitetura, Profit RTD, WebSocket e fontes por ativo.
14. Confirmar `Latencia WS`, `Msg/s` e contadores no `Sistema`.

## Build validado

O build foi validado com `csc.exe` do pacote `Microsoft.Net.Compilers.Toolset.4.8.0`. Para desenvolvimento normal, use Visual Studio 2022/Build Tools com restore NuGet.
