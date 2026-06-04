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
- HTML do `dolar-points` importado e adaptado com menu superior agrupado em `Inicio`, `Cadastro`, `Mercado`, `Fluxo`, `Analise` e `Sistema`.
- Tela `Painel` como entrada operacional com checklist, atalhos, setups, oportunidades e alertas.
- Tela `Radar` para oportunidades observacionais ranqueadas por setups, niveis, proximidade, delta e imbalance.
- Tela `Monitor` como mesa ao vivo com watchlist compacta, estado do ativo, setups, tape, oportunidades e alertas.
- Tela `Mesa` como cockpit de analise com DOM compacto, book resumido, tape, fluxo, setups e niveis proximos.
- Tela `Cotacoes` como watchlist operacional entre o cadastro de ativos e as telas de analise.
- Tela `Oportunidades` para ideias observacionais com preco de interesse, stop, alvo, risco simulado e status por toque de preco.
- Tela `Ajustes` para parametros locais de tick, DOM, renderizacao, memoria de tape/sinais e valor por ponto.
- Tela `Conexoes` para polling de `/health`, estado do coletor, Profit RTD, WebSocket e fontes por ativo.
- Hotbar operacional contextual por grupo, com memoria da ultima tela usada e atalhos `Alt+1` a `Alt+9` para telas de uso frequente.
- Paleta `Ctrl+K` para buscar telas e ativos cadastrados.
- Faixa superior operacional com ativo selecionado, ultimo preco, bid/ask, Book, Times, delta e CSV.
- Telemetria WebSocket no frontend com latencia local, mensagens por segundo, reconexoes e contadores por tipo de mensagem.
- Coalescing auxiliar para `bookDepth` e `timesTrades`, reduzindo repintura sem bloquear snapshots de preco.
- Scheduler de render no navegador para agrupar DOM, Painel, Cotacoes e Historico, com padrao de 120 ms e ajuste pela UI, mantendo inputs de preco imediatos.
- Validador `tools/validate-dashboard-design.js` para preservar tokens Industrial, mono, plano e sem sombras/gradientes.

## Fluxo de validacao

1. Abrir Profit Pro.
2. Compilar x64 no Visual Studio.
3. Rodar o exe e abrir `http://localhost:5000`.
4. Se o RTD nao conectar, compilar x86.
5. Cadastrar ativo em `Ativos` e carregar CSV historico.
6. Confirmar que os campos intraday sao preenchidos por RTD.
7. Adicionar outro ativo em `Ativos`, testar `Ver`, `Ligado`, `Desligado`, fontes e `Excluir`.
8. Abrir `Painel` e confirmar checklist, atalhos, setups, oportunidades e alertas.
9. Abrir `Radar` e confirmar candidatos por setup/nivel, score, evidencias e acao `Observar`.
10. Abrir `Mesa` e confirmar DOM compacto, book resumido, tape, fluxo, setups, niveis proximos e acoes de analise.
11. Abrir `Monitor` e confirmar watchlist compacta, estado do ativo, setups, tape, oportunidades e alertas.
12. Abrir `Cotacoes` e testar os atalhos para `Grafico`, `DOM`, `Book`, `T&T` e `Oportunidades`.
13. Salvar uma oportunidade observacional e confirmar que ela permanece local, sem comandos operacionais ao Profit.
14. Abrir `Book`, `T&T`, `Alertas`, `Risco`, `Historico`, `Ajustes`, `Conexoes` e `Sistema`.
15. Confirmar `Conexoes` com `/health`, arquitetura, Profit RTD, WebSocket e fontes por ativo.
16. Confirmar grupos superiores, hotbar contextual, memoria da ultima tela por grupo e atalhos `Alt+1` a `Alt+9`.
17. Confirmar paleta `Ctrl+K` para telas e ativos.
18. Confirmar `Latencia WS`, `Msg/s` e contadores no `Sistema`.
19. Rodar `node tools/validate-dashboard-design.js`.

## Build validado

O build foi validado com `csc.exe` do pacote `Microsoft.Net.Compilers.Toolset.4.8.0`. Para desenvolvimento normal, use Visual Studio 2022/Build Tools com restore NuGet.
