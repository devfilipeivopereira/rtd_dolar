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
- Servidor local `HttpListener` com `/`, `/health`, `/bootstrap`, `/snapshot`, `/flow`, `/signals`, `/assets`, `/assets/toggle`, `/assets/channels`, `/assets/delete`, `/assets/history` e `/ws`.
- WebSocket com broadcast de snapshots, status, flow, signals, `bookDepth` e `timesTrades`.
- SQLite auxiliar para snapshots de 1 segundo e consolidado por minuto.
- HTML do `dolar-points` importado e adaptado com menu superior agrupado em `Inicio`, `Cadastro`, `Mercado`, `Fluxo`, `Analise` e `Sistema`.
- Tela `Painel` como entrada de analise com leitura rapida de contexto, checklist, atalhos, setups, oportunidades e alertas.
- Tela `Radar` para oportunidades observacionais ranqueadas por setups, niveis, proximidade, delta e imbalance, com ranking multiativo.
- Tela `Monitor` como mesa ao vivo com watchlist compacta, estado do ativo, setups, tape, oportunidades e alertas.
- Tela `Mesa` como cockpit de analise com DOM compacto, book resumido, tape, fluxo, setups e niveis proximos.
- Tela `Cotacoes` como watchlist de mercado entre o cadastro de ativos e as telas de analise.
- Tela `Oportunidades` para ideias observacionais com preco de interesse, stop, alvo, risco simulado e status por toque de preco.
- Tela `Ajustes` com presets `Rapido`, `Equilibrado` e `Detalhado`, alem de parametros locais de tick, DOM, renderizacao, memoria de tape/sinais e valor por ponto.
- Tela `Conexoes` para polling de `/health`, estado do coletor, Profit RTD, WebSocket e fontes por ativo.
- Hotbar de analise contextual por grupo, com trilha `Grupo / Tela`, memoria da ultima tela usada e atalhos `Alt+1` a `Alt+9` para telas de uso frequente.
- Selos operacionais no menu superior, calculados por grupo a partir de feed, cadastro, fluxo, ideias/alertas e health.
- Paleta `Ctrl+K` para buscar grupos, telas e ativos cadastrados, com `Proximo passo`, status operacional, atalhos e feed/fontes por ativo.
- Faixa superior de contexto com ativo selecionado, ultimo preco, bid/ask, Book, Times, delta e CSV.
- `Score Quant` no `Painel`, com `Edge Quant`, `Gate Quant`, `EV Proxy`, `R/R Proxy`, `Amostra Quant`, `Indicadores Quant`, `Base Quant` e `Evidencias Quant` combinando CSV estatistico, RTD de preco, fluxo derivado/T&T, confluencia, backtest proxy e gate de edge.
- Telemetria no frontend com latencia WebSocket local, mensagens por segundo, render da UI, reconexoes, contadores por tipo de mensagem, contadores backend de WebSocket e saude do processo local.
- Roteiro de analise no `Painel`, com `Proximo passo` dinamico e etapas clicaveis para Ativo, RTD preco, CSV, Book/T&T, Fluxo e Score.
- Coalescing auxiliar para `bookDepth` e `timesTrades`, reduzindo repintura sem bloquear snapshots de preco.
- Scheduler de render no navegador orientado pela aba ativa, com fila por motivo/ativo, filtro de relevancia por tela, padrao de 120 ms no preset `Equilibrado`, presets de desempenho pela UI e inputs de preco imediatos.
- Bootstrap HTTP consolidado para abertura/reconexao, carregando health, assets, snapshot, flow e signals em uma unica resposta local, com fallback para endpoints antigos.
- Deteccao de feed parado com `lastUpdateAgeMs` no `/health`, metrica `Feed` na faixa superior e diagnostico `Idade backend` / `Feed selecionado` em `Conexoes`.
- Freshness por ativo em `/assets`, `Cotacoes` e `Conexoes`, com `feedStatus`, `lastUpdateAgeMs`, `lastPrice` e `hasPrice`.
- Penalizacao de freshness no `Score Quant`, no `Radar` e no ranking multiativo, para rebaixar feed atrasado/parado e expor a evidencia ao usuario.
- Validador `tools/validate-dashboard-design.js` para preservar tokens Industrial, mono, plano e sem sombras/gradientes.
- Validador `tools/validate-product-language.js` para preservar o foco em analise/oportunidades e bloquear linguagem de envio de operacoes.
- Validador `tools/validate-quant-surface.js` para preservar estimadores, indicadores, radar, score quant, gate de edge, EV/RR proxy, cap de score e evidencias visiveis.
- Validador `tools/validate-live-render-scheduler.js` para preservar coalescing de render, motivos, ativos e filtro por aba ativa.
- Validador `tools/validate-feed-freshness.js` para preservar idade de feed, status `Ao vivo`/`Atrasado`/`Parado` e diagnostico de freshness.
- Validador `tools/validate-bootstrap-loading.js` para preservar `/bootstrap` e fallback de carregamento inicial.
- Validador `tools/validate-websocket-health.js` para preservar clientes, broadcasts e falhas do WebSocket em `/health`.
- Validador `tools/validate-process-health.js` para preservar uptime, memoria, GC e threads do processo local em `/health`.

## Fluxo de validacao

1. Abrir Profit Pro.
2. Compilar x64 no Visual Studio.
3. Rodar o exe e abrir `http://localhost:5000`.
4. Se o RTD nao conectar, compilar x86.
5. Cadastrar ativo em `Ativos` e carregar CSV historico.
6. Confirmar que os campos intraday sao preenchidos por RTD.
7. Adicionar outro ativo em `Ativos`, testar `Ver`, `Ligado`, `Desligado`, fontes e `Excluir`.
8. Abrir `Painel` e confirmar contexto, fluxo, nivel proximo, radar, feed, checklist, atalhos, setups, oportunidades e alertas.
9. Abrir `Radar` e confirmar candidatos por setup/nivel, score, evidencias, ranking multiativo e acoes `Observar`, `Ver` e `Mesa`.
10. Abrir `Mesa` e confirmar DOM compacto, book resumido, tape, fluxo, setups, niveis proximos e acoes de analise.
11. Abrir `Monitor` e confirmar watchlist compacta, estado do ativo, setups, tape, oportunidades e alertas.
12. Abrir `Cotacoes` e testar os atalhos para `Grafico`, `DOM`, `Book`, `T&T` e `Oportunidades`.
13. Salvar uma oportunidade observacional e confirmar que ela permanece local, sem envio ao Profit.
14. Abrir `Book`, `T&T`, `Alertas`, `Risco`, `Historico`, `Ajustes`, `Conexoes` e `Sistema`.
15. Em `Ajustes`, alternar `Rapido`, `Equilibrado` e `Detalhado` e confirmar DOM/render/memoria no resumo.
16. Confirmar `Conexoes` com `/health`, arquitetura, Profit RTD, WebSocket e fontes por ativo.
17. Confirmar grupos superiores, selos operacionais, trilha `Grupo / Tela`, hotbar contextual, memoria da ultima tela por grupo e atalhos `Alt+1` a `Alt+9`.
18. Confirmar paleta `Ctrl+K` para grupos, telas, ativos, `Proximo passo`, status operacional e feed/fontes.
19. Confirmar `Latencia WS`, `Msg/s`, `Render UI` e contadores no `Sistema`.
20. Rodar `node tools/validate-dashboard-design.js`.
21. Rodar `node tools/validate-product-language.js`.
22. Rodar `node tools/validate-quant-surface.js`.
23. Rodar `node tools/validate-live-render-scheduler.js`.
24. Rodar `node tools/validate-feed-freshness.js`.
25. Rodar `node tools/validate-bootstrap-loading.js`.
26. Rodar `node tools/validate-websocket-health.js`.
27. Rodar `node tools/validate-process-health.js`.
28. Confirmar `GET /bootstrap` retornando health, assets, snapshot, flow e signals.
29. Confirmar `/health.webSocket` com clientes, broadcasts e falhas.
30. Confirmar `/health.process` com uptime, memoria e threads.
31. Confirmar `Score Quant`, `Edge Quant`, `Gate Quant`, `EV Proxy`, `R/R Proxy`, `Amostra Quant`, `Indicadores Quant`, `Base Quant` e `Evidencias Quant` no `Painel`.
32. Confirmar o roteiro de analise com `Proximo passo` e atalhos para Ativos, Conexoes, Fluxo, Radar e Mesa.
33. Confirmar `Render motivos`, `Render ativos`, contadores backend de WebSocket e saude do processo no `Sistema`.
34. Confirmar `Feed`, `Idade backend`, `Feed selecionado` e Feed por ativo em tempo real.
35. Confirmar que `Score Quant` e `Radar` reduzem confianca quando o feed fica `Atrasado` ou `Parado`.

## Build validado

O build foi validado com `csc.exe` do pacote `Microsoft.Net.Compilers.Toolset.4.8.0`. Para desenvolvimento normal, use Visual Studio 2022/Build Tools com restore NuGet.
