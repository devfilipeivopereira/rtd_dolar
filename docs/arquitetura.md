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
- `Web/`: `HttpListener`, arquivos estaticos, `/health`, `/bootstrap`, `/snapshot`, `/flow`, `/signals`, `/assets` e `/ws`.
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

## Motor Quant e Fluxo

O motor quant usa duas familias de entrada. O CSV historico alimenta volatilidade Garman-Klass, Parkinson, Rogers-Satchell, Yang-Zhang, ATR, z-score, regime de mercado, profile proxy, POC, VAH/VAL, confluencias e backtest proxy. O RTD alimenta preco atual, abertura, maxima/minima parcial, VWAP/MED, book, Times & Trades, delta, imbalance, microprice, VWAP derivada e tape.

O `Score Quant` no `Painel` consolida essas entradas em uma leitura observacional de reversao ou continuidade. Ele pondera nivel/proximidade, regime estatistico, backtest proxy, z-score e alinhamento de fluxo. Quando falta CSV, preco RTD ou fluxo/T&T, o score e penalizado e a `Base Quant` mostra a falta de fonte. Quando o snapshot do ativo fica atrasado ou parado, o score recebe penalizacao adicional, a qualidade muda para `feed atrasado` ou `feed parado` e as evidencias exibem a idade do feed. A interface tambem exibe `Indicadores Quant` e `Evidencias Quant`, para que o usuario veja de onde saiu o numero.

Acima do score bruto existe um gate de edge. Ele usa amostra minima de 21 pregoes, taxa de reversao do backtest proxy, acordo entre Garman-Klass/Yang-Zhang/Parkinson/Rogers-Satchell/ATR, R/R proxy, EV proxy, fluxo e freshness do feed. O `Painel` expoe `Edge Quant`, `Gate Quant`, `EV Proxy`, `R/R Proxy` e `Amostra Quant`; o `Radar` aplica a mesma regra e limita scores quando o edge fica em teste ou bloqueado. Assim o RTD serve como dado vivo de preco/fluxo, mas a leitura final continua dependente de contexto estatistico auditavel.

Esse motor e analitico: ele procura pontos de reversao e oportunidades por estatistica, indicador tecnico e leitura de fluxo, sem promessa de resultado financeiro e sem envio ao Profit.

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
- `Sistema`: saude, telemetria WebSocket, render da UI e debug.

A faixa superior fica disponivel em todas as telas e mostra ativo selecionado, ultimo preco, bid/ask, status de Book, status de Times, delta 5s, latencia WebSocket local, mensagens por segundo, render da UI e CSV carregado.

Cada grupo do menu superior tem um selo operacional calculado no navegador a partir do estado real: freshness do feed, fontes do ativo, fluxo, ideias/alertas e health. Assim o usuario sabe se precisa ir para `Cadastro`, `Fluxo` ou `Sistema` antes de abrir a tela.

A hotbar de analise fica abaixo da faixa superior e abre telas de uso frequente por clique ou `Alt+1` a `Alt+9`. Ela tambem mostra a trilha `Grupo / Tela`, o estado operacional da tela ativa e um botao `Proximo` calculado pela mesma logica do roteiro do `Painel`. O estado muda conforme o modulo aberto: feed/preco, Book, T&T, fluxo, setups, ideias, alertas, health ou diagnostico do terminal. Os badges clicaveis `P`, `B`, `T`, `CSV`, `Flow` e `Edge` indicam prontidao por fonte do ativo selecionado e abrem a tela de correcao/inspecao mais provavel. O resumo e atualizado com throttle para nao competir com DOM, Book e T&T em tempo real.

A paleta `Ctrl+K` busca grupos, telas e ativos cadastrados. Ela mostra `Proximo passo`, status operacional dos grupos, atalhos das telas e feed/fontes `P/B/T` dos ativos. Quando o item escolhido e um ativo, a UI seleciona o ativo e abre `Monitor`.

A tela `Painel` e a entrada de analise. Ela resume RTD, ativo selecionado, ultimo preco, leitura rapida de contexto, fluxo, nivel proximo, radar e feed, alem de checklist de prontidao, atalhos para as telas principais, setups recentes, oportunidades e alertas.

Logo abaixo dos cards de contexto, o `Painel` mostra um roteiro de analise com `Proximo passo` e etapas clicaveis: `Ativo`, `RTD preco`, `CSV`, `Book/T&T`, `Fluxo` e `Score`. A decisao e baseada no estado real do ativo selecionado. Sem cadastro, abre `Ativos`; sem preco, abre `Conexoes`; sem CSV, volta para `Ativos`; sem fluxo, abre `Fluxo`; com score utilizavel, abre `Radar`; caso contrario, abre `Mesa`.

A tela `Radar` ranqueia oportunidades observacionais por setups, niveis, proximidade, delta e imbalance. O ranking usa a freshness do feed como parte do score: ativo com preco fresco pode subir, ativo atrasado ou parado e rebaixado e mostra a evidencia do feed. Tambem mostra ativos em atencao para troca rapida de contexto.

A tela `Monitor` e a mesa de acompanhamento ao vivo. Ela junta watchlist compacta, estado do ativo selecionado, setups ativos, tape, oportunidades e alertas sem editar cadastro nem parametros.

A tela `Mesa` concentra DOM compacto, book resumido, tape, fluxo, setups, niveis proximos e acoes de analise.

A tela `Ativos` configura fontes e CSV. A tela `Cotacoes` e a mesa de monitoramento: cada linha mostra ultimo preco, feed/freshness por ativo, bid/ask, delta, status de Book, status de Times, status das fontes `P/B/T` e botoes para abrir `Grafico`, `DOM`, `Book`, `T&T` ou `Oportunidades` daquele ativo.

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

O scheduler do navegador agora acumula motivos de render (`snapshot`, `book`, `times`, `flow`, `signal`, `status`, `ui`) e ativos impactados. Antes de redesenhar, ele verifica se o evento afeta a tela ativa. Por exemplo, uma mensagem `flow` nao repinta a tela `Book`, e uma mensagem `book` nao repinta a tela `T&T`. Telas de contexto amplo como `Painel`, `Mesa`, `Monitor`, `Radar`, `Cotacoes`, `Conexoes` e `Sistema` continuam recebendo todos os motivos relevantes.

Na abertura e na reconexao do WebSocket, o navegador tenta `GET /bootstrap`. Esse endpoint devolve em uma unica resposta local: health, assets, snapshot atual, flow atual e signals recentes. Se falhar, o dashboard usa os endpoints antigos `/flow`, `/signals` e `/assets`, mantendo compatibilidade.

O backend inclui `lastUpdateAgeMs` em `/health` e tambem envia `lastUpdate`, `lastUpdateAgeMs`, `lastPrice`, `hasPrice` e `feedStatus` em cada ativo de `/assets`. O mesmo `/health` expoe `webSocket` com clientes conectados, broadcasts, mensagens alvo, falhas de envio e ultimo broadcast, alem de `process` com PID, uptime, memoria, GC e threads. No navegador, a faixa superior calcula a idade do snapshot do ativo selecionado e classifica o feed como `Ao vivo`, `Atrasado`, `Parado`, `Sem preco` ou `Manual`. `Cotacoes` e `Conexoes` repetem essa leitura por ativo. O polling de `/health` tambem aciona um render `status`, para que a UI indique feed parado mesmo quando nenhuma nova mensagem chega pelo WebSocket. A mesma freshness alimenta o `Score Quant` e o `Radar`, evitando score alto baseado em preco congelado.

A mesma faixa superior mostra um selo compacto `Terminal OK`, `Terminal Atencao` ou `Terminal Alerta`. Ele consolida WebSocket local, freshness do feed, latencia coletor-navegador, render da UI, fila do fluxo e memoria do processo em uma leitura unica, com causas curtas como `feed`, `ws`, `lat`, `ui`, `fila` ou `mem`.

A telemetria `Latencia WS` e calculada no navegador comparando o recebimento da mensagem com `localTimestamp` enviado pelo backend local. A metrica `Render UI` mede a duracao do ultimo batch de desenho da aba ativa, com media e pico no `Sistema`. `Conexoes` e `Sistema` tambem exibem contadores backend de WebSocket e saude do processo local para diferenciar problema de navegador, cliente desconectado, falha de broadcast e coletor pesado. Essas leituras servem para diagnosticar atraso entre coletor, navegador e interface; nao medem latencia de bolsa ou Profit.

## Persistencia

SQLite e auxiliar. Se o provider SQLite falhar, o RTD e o WebSocket continuam rodando.

Tabelas:

- `snapshots`: ultimo estado consolidado salvo por intervalo.
- `minute_snapshots`: ultimo estado de cada minuto por ativo.
