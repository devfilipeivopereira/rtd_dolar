# ColetorProfitRTD

Aplicacao local Windows para ler RTD do Profit Pro sem Excel e alimentar o HTML do `dolar-points` em tempo real.

Repositorio alvo: https://github.com/devfilipeivopereira/rtd_dolar

## Documentacao

- [Arquitetura](docs/arquitetura.md)
- [Campos RTD](docs/campos_rtd.md)
- [Validacao e troubleshooting](docs/validacao.md)
- [Plano implementado](docs/plano_implementado.md)
- [Plano do novo projeto nativo low-latency](docs/plano_novo_projeto_nativo_low_latency.md)
- [Tape reading e order flow](docs/order_flow.md)

## Arquitetura

```text
Profit Pro conectado
  -> RTDTrading.RTDServer
  -> Coletor C# .NET Framework 4.8
  -> http://localhost:5000
  -> WDO_Reversal_Engine.html adaptado com RTD Live
```

O coletor pode assinar varios ativos RTD ao mesmo tempo. No dashboard, a secao `Ativos RTD` permite adicionar um simbolo, escolher qual ativo a tela esta exibindo, ligar/desligar, excluir e escolher quais canais RTD ficam ativos para cada simbolo:

- `Preco`: ativo do Profit, campos de preco/indicadores e intraday;
- `Book`: topico de book como `BOOK0`, com profundidade padrao de 50 niveis;
- `Times`: topico Times & Trades como `T&T0`, com janela padrao de 100 linhas.

O cadastro de fontes fica na aba principal `Ativos`. O app salva a configuracao em `data/assets/assets.json` e o CSV historico em `data/assets/{ativo}/history.csv`.

O historico diario continua sendo carregado por CSV no navegador. O RTD preenche os campos intraday usados pelo motor quant:

- Abertura: `ABE`
- Maxima parcial: `MAX`
- Minima parcial: `MIN`
- Preco atual: `ULT`
- VWAP/ancora opcional: `MED`
- Volume acumulado: `VOL`

O menu superior agora separa o terminal em grupos de analise: `Inicio`, `Cadastro`, `Mercado`, `Fluxo`, `Analise` e `Sistema`. A hotbar contextual abaixo mostra apenas as telas do grupo ativo, enquanto a faixa superior mantem ativo selecionado, ultimo preco, bid/ask, status de Book, status de Times, delta, latencia WebSocket local, mensagens por segundo e CSV:

- `Painel`: entrada de analise com checklist, atalhos, setups, oportunidades e alertas;
- `Radar`: oportunidades observacionais ranqueadas por setup, nivel, proximidade, delta e imbalance, com ranking multiativo;
- `Monitor`: mesa ao vivo com watchlist compacta, estado do ativo, setups, tape, oportunidades e alertas;
- `Mesa`: cockpit de analise com DOM compacto, book resumido, tape, fluxo, setups e risco simulado;
- `Ativos`: cadastro, CSV historico, ligar/desligar e excluir;
- `Cotacoes`: watchlist de mercado com todos os ativos cadastrados, ultimo preco, bid/ask, delta, Book, Times e atalhos;
- `Grafico`: grafico, niveis, abertura, POC, variacao, profile e backtest;
- `DOM`: escada de preco e pontos principais;
- `Book`: book de ofertas RTD em tabela de compra e venda;
- `T&T`: Times & Trades RTD;
- `Fluxo`: delta, book, tape e metricas de order flow;
- `Setups`: sinais ativos e recentes;
- `Oportunidades`: ideias observacionais com preco de interesse, stop, alvo, risco simulado e status por toque de preco;
- `Alertas`: alertas locais de preco por ativo;
- `Risco`: calculadora local de stop, alvo e tamanho simulado;
- `Historico`: resumo do CSV e ticks em memoria;
- `Ajustes`: presets `Rapido`, `Equilibrado` e `Detalhado`, parametros locais de tick, DOM, renderizacao, memoria de tape/sinais e valor por ponto;
- `Conexoes`: estado do coletor, Profit RTD, WebSocket, `/health` e fontes por ativo;
- `Sistema`: RTD, WebSocket, telemetria de mensagens e debug de fluxo.

A hotbar lembra a ultima tela usada dentro de cada grupo e ainda abre telas frequentes por teclado: `Alt+1` Monitor, `Alt+2` DOM, `Alt+3` Book, `Alt+4` T&T, `Alt+5` Fluxo, `Alt+6` Oportunidades, `Alt+7` Ativos, `Alt+8` Conexoes e `Alt+9` Sistema.

`Ctrl+K` abre a paleta de busca para localizar telas e ativos cadastrados.

A aba `DOM` mostra:

- escada de preco em ticks de 0,5 ponto ao redor do ultimo preco;
- tape recente com movimento tick a tick;
- bid/ask do RTD quando `OCP`, `OVD`, `VOC` e `VOV` estiverem disponiveis;
- marcacoes de pontos principais como abertura, maxima, minima, VWAP/MED, POC, VAH/VAL, desvios e confluencias.

## Build

Prerequisitos:

- Windows 10/11
- Visual Studio 2022 ou Build Tools
- .NET Framework 4.8 Developer Pack
- Profit Pro instalado, aberto e logado

Abra `ColetorProfitRTD.sln` no Visual Studio e compile primeiro em `x64`.

Se aparecer `Class not registered` ou `RTDTrading.RTDServer nao encontrado`, compile em `x86` e teste novamente. A arquitetura precisa bater com o registro COM do RTD do Profit.

Use o MSBuild do Visual Studio 2022/Build Tools. O MSBuild antigo de `C:\Windows\Microsoft.NET\Framework*\v4.0.30319` nao entende `PackageReference` e falha antes da compilacao.

## Como rodar

1. Abra o Profit Pro e deixe conectado.
2. Execute `ColetorProfitRTD.exe`.
3. Abra `http://localhost:5000`.
4. Abra `Ativos`.
5. Cadastre o codigo de preco, por exemplo `WDON26_G_0`.
6. Configure `BOOK0` e `T&T0`, ou os topicos equivalentes do Profit.
7. Carregue o CSV historico do ativo.
8. Clique em `Salvar`.
9. Abra `Painel` para ver checklist de analise, atalhos, setups, oportunidades e alertas.
10. Abra `Radar` para ver oportunidades observacionais ranqueadas por contexto e ativos em atencao.
11. Abra `Mesa` para analisar DOM, book, tape, fluxo, setups e risco simulado em uma tela integrada.
12. Abra `Monitor` para acompanhar watchlist, setups, tape, oportunidades e alertas do ativo selecionado.
13. Abra `Cotacoes` para ver todos os ativos cadastrados e entrar em `Grafico`, `DOM`, `Book`, `T&T` ou `Oportunidades`.
14. Abra `Ajustes` se quiser alternar entre `Rapido`, `Equilibrado` e `Detalhado`, ou mudar niveis do DOM, cadencia de renderizacao e valor por ponto padrao.
15. Abra `Conexoes` para confirmar coletor, Profit RTD, WebSocket, arquitetura e fontes por ativo.
16. Deixe o modo `RTD Live` ativo para preencher o intraday automaticamente.
17. Confira `Latencia WS` e `Msg/s` na faixa superior ou em `Sistema` para diagnosticar atraso entre backend local e navegador.
18. Use os grupos superiores, a hotbar contextual, `Alt+1` a `Alt+9` ou `Ctrl+K` para alternar telas e selecionar ativos rapidamente.

O aplicativo e somente para analise e busca de oportunidades. Ele nao envia nada ao Profit nem controla execucao.

Para uma prova minima sem dashboard:

```text
ColetorProfitRTD.exe --probe
```

Esse modo assina apenas `WDOFUT_F_0 / VOL` por 30 segundos.

Endpoints:

```text
GET /health
GET /snapshot
GET /flow
GET /signals
GET /assets
POST /assets
POST /assets/toggle
POST /assets/channels
POST /assets/delete
POST /assets/history
GET /assets/history?asset=WDON26_G_0
DELETE /assets
WS  /ws
```

## Configuracao

Edite `src/ColetorProfitRTD/appsettings.json`:

- `Rtd.Asset`: ativo RTD padrao usado como semente quando ainda nao existe `data/assets/assets.json`
- `Rtd.Assets`: ativos cadastrados ao primeiro iniciar
- `Rtd.ActiveAssets`: ativos que ja iniciam ligados
- `Rtd.AssetChannels`: canais habilitados na importacao inicial
- `Rtd.ChannelFields`: compatibilidade com o modo simples antigo
- `Rtd.Fields`: compatibilidade com o modo simples antigo
- `Web.HttpPort`: porta HTTP/WebSocket
- `Storage.Enabled`: liga/desliga SQLite auxiliar

Depois que a tela `Ativos` salva a primeira configuracao, o runtime passa a usar `data/assets/assets.json`.

Exemplo para iniciar com dois ativos cadastrados, mas apenas WDO ligado:

```json
"Rtd": {
  "Asset": "WDOFUT_F_0",
  "Assets": ["WDOFUT_F_0", "WINFUT_F_0"],
  "ActiveAssets": ["WDOFUT_F_0"],
  "AssetChannels": {
    "WDOFUT_F_0": ["price", "book", "timesTrades"],
    "WINFUT_F_0": ["price", "book"]
  }
}
```

O catalogo de campos conhecidos fica em `src/ColetorProfitRTD/Rtd/RtdFieldCatalog.cs`.

## Estrutura

```text
ColetorProfitRTD.sln
src/
  ColetorProfitRTD/     backend C# RTD, WebSocket, HTTP e SQLite
  dashboard/            HTML do dolar-points adaptado com RTD Live e DOM
docs/                   documentacao de uso e validacao
data/                   SQLite em runtime
logs/                   logs em runtime
```

## Testes manuais essenciais

1. Profit fechado: `/health` deve mostrar RTD desconectado e o HTML deve exibir reconexao.
2. Profit aberto: logs devem mostrar `ServerStart retornou 1` e campos assinados.
3. x64/x86: testar ambas se o COM nao registrar na primeira arquitetura.
4. CSV + RTD: carregar CSV e confirmar que o RTD atualiza os campos intraday.
5. Manual: desligar `RTD Live` e editar os campos manualmente.
6. Multiativo: adicionar um novo ativo em `Ativos`, ligar/desligar, trocar fontes, excluir e confirmar `/assets`.
7. Book/T&T: confirmar mensagens `bookDepth` e `timesTrades` no WebSocket quando `BOOK0` e `T&T0` estiverem ligados.
8. Painel: confirmar checklist, atalhos, setups, oportunidades e alertas do ativo selecionado.
9. Radar: confirmar candidatos por setup/nivel, score, evidencias, ranking multiativo e atalhos `Observar`, `Ver` e `Mesa`.
10. Mesa: confirmar DOM compacto, book resumido, tape, fluxo, setups, niveis proximos e botoes para `Oportunidades`, `Risco`, `Alertas` e `Grafico`.
11. Monitor: confirmar watchlist compacta, estado do ativo, setups, tape, oportunidades e alertas.
12. Cotacoes: confirmar que a watchlist mostra todos os ativos e que os botoes abrem `Grafico`, `DOM`, `Book`, `T&T` e `Oportunidades`.
13. Oportunidades: salvar uma ideia observacional e confirmar status `monitorando`, `observado`, `alvo` ou `stop` conforme o preco.
14. Abas de analise: confirmar `Book`, `T&T`, `Alertas`, `Risco`, `Historico`, `Ajustes`, `Conexoes` e `Sistema` sem erro no navegador.
15. Conexoes: confirmar polling de `/health`, arquitetura x64/x86, Profit RTD, WebSocket e status `Preco`, `Book`, `Times` por ativo.
16. Ajustes: alternar presets `Rapido`, `Equilibrado` e `Detalhado`; depois alterar niveis do DOM e intervalo de renderizacao, salvar, recarregar a pagina e confirmar persistencia local.
17. Navegacao: confirmar grupos superiores, hotbar contextual, memoria da ultima tela por grupo e atalhos `Alt+1` a `Alt+9`, sem disparar quando o foco esta em campos de texto.
18. Paleta: confirmar `Ctrl+K`, busca de telas, busca de ativos, setas, `Enter` e `Esc`.
19. Performance: confirmar que DOM, Mesa, Painel, Monitor e Cotacoes seguem responsivos com RTD ativo; campos intraday devem atualizar imediatamente.
20. Telemetria: confirmar que `Latencia WS` e `Msg/s` mudam quando chegam mensagens WebSocket. Essa leitura mede o trecho backend local -> navegador, nao latencia de bolsa ou Profit.
21. Design QA: rodar `node tools/validate-dashboard-design.js` e confirmar `Dashboard design tokens OK`.
22. Linguagem de produto: rodar `node tools/validate-product-language.js` e confirmar `Product language OK`.
23. SQLite: confirmar criacao de `data/marketdata.sqlite` quando o provider for restaurado pelo NuGet.
