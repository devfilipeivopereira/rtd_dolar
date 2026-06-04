# Arquitetura

## Objetivo

O projeto transforma o RTD do Profit em uma fonte local de dados em tempo real para uma interface HTML. O Excel nao entra no fluxo: o aplicativo C# assume o papel de cliente RTD.

```text
Profit Pro
  -> RTDTrading.RTDServer
  -> RtdClient C# em thread STA
  -> MarketSnapshot / BookDepth / TimesTrades por ativo
  -> WebSocket ws://localhost:5000/ws
  -> Dashboard HTML
```

## Backend

O backend fica em `src/ColetorProfitRTD`.

- `Rtd/`: interfaces COM, catalogo de campos e cliente RTD.
- `MarketData/`: parsing pt-BR e snapshots consolidados por ativo.
- `Web/`: `HttpListener`, arquivos estaticos, `/health`, `/snapshot`, `/flow`, `/signals`, `/assets` e `/ws`.
- `Storage/`: SQLite auxiliar para snapshots e buckets por minuto.
- `Flow/`: order flow derivado por ativo, com delta, VWAP, profile intraday e sinais.

O RTD roda em uma thread STA dedicada. Isso evita misturar chamadas COM com o loop HTTP/WebSocket.

O cadastro de ativos tem dois niveis:

- `appsettings.json`: semente inicial quando ainda nao existe cadastro persistido;
- `data/assets/assets.json`: cadastro runtime salvo pela tela `Ativos`.

Durante a execucao, o dashboard chama `POST /assets` para criar/atualizar ativo e `POST /assets/toggle` para ligar/desligar o ativo inteiro ou uma fonte especifica. Desligar remove os topicos RTD daquele ativo no servidor COM.

Tambem existem:

- `POST /assets/channels`: troca os canais RTD assinados para um ativo;
- `POST /assets/delete` ou `DELETE /assets`: remove o ativo da lista, desassina os topicos e limpa o snapshot em memoria.
- `POST /assets/history`: salva o CSV historico do ativo em `data/assets/{ativo}/history.csv`;
- `GET /assets/history?asset=...`: devolve o CSV salvo para recarregar o Panorama.

Os canais atuais sao:

- `price`: topico do ativo, como `WDON26_G_0`;
- `book`: topico `BOOKn`, com `INFO/ATV`, `INFO/TAB` e campos por nivel;
- `timesTrades`: topico `T&Tn`, com `INFO/ATV`, `INFO/TAB` e campos por linha.

O cliente RTD aceita argumentos variaveis. Preco usa `ativo, campo`; book usa `topico, campo, nivel`; Times & Trades usa `topico, campo, linha`.

## Frontend

O frontend fica em `src/dashboard/index.html` e usa o HTML do `dolar-points` como base.

O CSV diario continua sendo a fonte do historico 21/45/63. O RTD preenche o intraday:

- abertura;
- maxima parcial;
- minima parcial;
- preco atual;
- VWAP/ancora opcional;
- volume acumulado;
- book e volume projetado.

O menu superior separa:

- `Painel`: leitura rapida de contexto, checklist de analise, atalhos e resumo do ativo selecionado;
- `Radar`: oportunidades observacionais por ativo e ranking multiativo;
- `Monitor`: mesa ao vivo com watchlist compacta, estado do ativo, setups, tape, oportunidades e alertas;
- `Mesa`: cockpit de analise com DOM compacto, book resumido, tape, fluxo, setups e niveis proximos;
- `Ativos`: cadastro de codigo, tres fontes RTD e CSV historico;
- `Cotacoes`: watchlist de mercado com todos os ativos cadastrados;
- `Grafico`: grafico e subabas analiticas;
- `DOM`: escada de preco;
- `Book`: book de ofertas multi-nivel;
- `T&T`: Times & Trades RTD;
- `Fluxo`: delta, book, tape e metricas;
- `Setups`: sinais;
- `Oportunidades`: ideias observacionais com preco de interesse, stop, alvo, risco simulado e acompanhamento por toque de preco;
- `Alertas`: alertas locais por preco;
- `Risco`: calculadora local de stop, alvo e contratos;
- `Historico`: resumo do CSV e ticks em memoria;
- `Ajustes`: presets de desempenho, parametros locais de tick, DOM, renderizacao, memoria e valor por ponto;
- `Conexoes`: estado do coletor, Profit RTD, WebSocket, `/health` e fontes por ativo;
- `Sistema`: saude, telemetria WebSocket e debug.

A faixa superior fica disponivel em todas as telas e mostra ativo selecionado, ultimo preco, bid/ask, status de Book, status de Times, delta 5s, latencia WebSocket local, mensagens por segundo e CSV carregado.

A hotbar de analise fica abaixo da faixa superior e abre telas de uso frequente por clique ou `Alt+1` a `Alt+9`. Ela acelera a leitura sem substituir o menu superior, que continua agrupando todas as funcionalidades.

A paleta `Ctrl+K` busca telas e ativos cadastrados. Quando o item escolhido e um ativo, a UI seleciona o ativo e abre `Monitor`.

A tela `Painel` e a entrada de analise. Ela resume RTD, ativo selecionado, ultimo preco, leitura rapida de contexto, fluxo, nivel proximo, radar e feed, alem de checklist de prontidao, atalhos para as telas principais, setups recentes, oportunidades e alertas.

A tela `Radar` ranqueia oportunidades observacionais por setups, niveis, proximidade, delta e imbalance. Tambem mostra ativos em atencao para troca rapida de contexto.

A tela `Monitor` e a mesa de acompanhamento ao vivo. Ela junta watchlist compacta, estado do ativo selecionado, setups ativos, tape, oportunidades e alertas sem editar cadastro nem parametros.

A tela `Mesa` concentra DOM compacto, book resumido, tape, fluxo, setups, niveis proximos e acoes de analise.

A tela `Ativos` configura fontes e CSV. A tela `Cotacoes` e a mesa de monitoramento: cada linha mostra ultimo preco, bid/ask, delta, status de Book, status de Times, status das fontes `P/B/T` e botoes para abrir `Grafico`, `DOM`, `Book`, `T&T` ou `Oportunidades` daquele ativo.

A tela `Oportunidades` persiste ideias observacionais no navegador por ativo. Ela calcula risco simulado e R/R, acompanha se preco de interesse, alvo ou stop foram tocados pelo RTD e permanece local.

A tela `Ajustes` persiste preferencias no navegador em `wdo-ui-settings`. Ela oferece presets `Rapido`, `Equilibrado` e `Detalhado` para controlar DOM, cadencia de renderizacao e memoria de tape/sinais de uma vez. Tambem permite ajuste fino de tamanho do tick, quantidade de niveis do DOM, intervalo de renderizacao, limite de trades/sinais em memoria e valor por ponto padrao para `Oportunidades` e `Risco`.

A tela `Conexoes` consulta `/health` a cada 3 segundos. Ela mostra status do coletor, Profit RTD, arquitetura, ultimo update, estado do WebSocket e status `Preco`, `Book` e `Times` por ativo.

A selecao de ativo define o que aparece nos campos intraday, DOM, fluxo e setups. Snapshots, book e Times & Trades de ativos diferentes ficam em caches separados no navegador, e as fontes podem ser ajustadas por ativo sem reiniciar o app.

## Aba DOM

A aba `DOM` mostra:

- escada em tick size configuravel, com padrao de 0,5;
- ultimo preco ao centro;
- bid/ask do snapshot de preco;
- volumes de book multi-nivel quando `bookDepth` chega;
- tape real quando `timesTrades` chega, com fallback para tape derivado;
- pontos principais do motor quant quando o CSV ja foi carregado.

Os pontos marcados incluem abertura, maxima, minima, VWAP/MED, POC, VAH, VAL, desvios por abertura, desvios por POC e confluencias.

As mensagens auxiliares de profundidade sao coalescidas no cliente RTD para reduzir repintura da UI: `bookDepth` tem broadcast minimo de 100 ms e `timesTrades` de 150 ms. Snapshots de preco continuam no fluxo existente.

No navegador, campos criticos de preco e inputs intraday sao preenchidos a cada snapshot. Renderizacoes densas usam scheduler curto, com padrao de 120 ms no preset `Equilibrado` e ajuste pela aba `Ajustes`. O batch ao vivo renderiza a aba ativa e evita repintar telas invisiveis como DOM, Mesa, Painel, Monitor, Cotacoes e Historico no mesmo pulso.

A telemetria `Latencia WS` e calculada no navegador comparando o recebimento da mensagem com `localTimestamp` enviado pelo backend local. Ela serve para diagnosticar atraso entre coletor e interface; nao mede latencia de bolsa ou Profit.

## Persistencia

SQLite e auxiliar. Se o provider SQLite falhar, o RTD e o WebSocket continuam rodando.

Tabelas:

- `snapshots`: ultimo estado consolidado salvo por intervalo.
- `minute_snapshots`: ultimo estado de cada minuto por ativo.
