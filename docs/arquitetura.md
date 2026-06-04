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

- `Painel`: checklist operacional, atalhos e resumo do ativo selecionado;
- `Monitor`: mesa ao vivo com watchlist compacta, estado do ativo, setups, tape, planos e alertas;
- `Ativos`: cadastro de codigo, tres fontes RTD e CSV historico;
- `Cotacoes`: watchlist operacional com todos os ativos cadastrados;
- `Grafico`: grafico e subabas analiticas;
- `DOM`: escada de preco;
- `Book`: book de ofertas multi-nivel;
- `T&T`: Times & Trades RTD;
- `Fluxo`: delta, book, tape e metricas;
- `Setups`: sinais;
- `Boleta`: plano local de entrada, stop, alvo e acompanhamento por toque de preco;
- `Alertas`: alertas locais por preco;
- `Risco`: calculadora local de stop, alvo e contratos;
- `Historico`: resumo do CSV e ticks em memoria;
- `Ajustes`: parametros locais de tick, DOM, renderizacao, memoria e valor por ponto;
- `Sistema`: saude, telemetria WebSocket e debug.

A faixa superior fica disponivel em todas as telas e mostra ativo selecionado, ultimo preco, bid/ask, status de Book, status de Times, delta 5s, latencia WebSocket local, mensagens por segundo e CSV carregado.

A tela `Painel` e a entrada operacional. Ela resume RTD, ativo selecionado, ultimo preco, checklist de prontidao, atalhos para as telas principais, setups recentes, planos da boleta e alertas.

A tela `Monitor` e a mesa de acompanhamento ao vivo. Ela junta watchlist compacta, estado do ativo selecionado, setups ativos, tape, planos e alertas sem editar cadastro nem parametros.

A tela `Ativos` configura fontes e CSV. A tela `Cotacoes` e a mesa de monitoramento: cada linha mostra ultimo preco, bid/ask, delta, status de Book, status de Times, status das fontes `P/B/T` e botoes para abrir `Grafico`, `DOM`, `Book`, `T&T` ou `Boleta` daquele ativo.

A `Boleta` persiste planos no navegador por ativo. Ela calcula risco e R/R, acompanha se entrada, alvo ou stop foram tocados pelo preco RTD e nao chama nenhum endpoint de envio de ordem.

A tela `Ajustes` persiste preferencias no navegador em `wdo-ui-settings`. Ela controla tamanho do tick, quantidade de niveis do DOM, intervalo de renderizacao, limite de trades/sinais em memoria e valor por ponto padrao para `Boleta` e `Risco`.

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

No navegador, campos criticos de preco e inputs intraday sao preenchidos a cada snapshot. Renderizacoes densas como DOM completo, `Painel`, `Monitor`, `Cotacoes` e `Historico` usam scheduler curto, com padrao de 120 ms e ajuste pela aba `Ajustes`, para reduzir travamentos quando o RTD envia muitos updates.

A telemetria `Latencia WS` e calculada no navegador comparando o recebimento da mensagem com `localTimestamp` enviado pelo backend local. Ela serve para diagnosticar atraso entre coletor e interface; nao mede latencia de bolsa, Profit ou execucao de ordem.

## Persistencia

SQLite e auxiliar. Se o provider SQLite falhar, o RTD e o WebSocket continuam rodando.

Tabelas:

- `snapshots`: ultimo estado consolidado salvo por intervalo.
- `minute_snapshots`: ultimo estado de cada minuto por ativo.
