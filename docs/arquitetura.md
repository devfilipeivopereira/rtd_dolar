# Arquitetura

## Objetivo

O projeto transforma o RTD do Profit em uma fonte local de dados em tempo real para uma interface HTML. O Excel nao entra no fluxo: o aplicativo C# assume o papel de cliente RTD.

```text
Profit Pro
  -> RTDTrading.RTDServer
  -> RtdClient C# em thread STA
  -> MarketSnapshot por ativo
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

- `Rtd.Assets`: ativos conhecidos ao iniciar;
- `Rtd.ActiveAssets`: ativos que ja iniciam assinados.

Durante a execucao, o dashboard chama `POST /assets` para adicionar ativo e `POST /assets/toggle` para ligar/desligar a assinatura daquele simbolo. Desligar remove os topicos RTD daquele ativo no servidor COM.

Tambem existem:

- `POST /assets/channels`: troca os canais RTD assinados para um ativo;
- `POST /assets/delete` ou `DELETE /assets`: remove o ativo da lista, desassina os topicos e limpa o snapshot em memoria.

Os canais atuais sao:

- `quote`: cotacao e campos intraday principais;
- `book`: topo de book por `OCP`, `OVD`, `VOC`, `VOV` e `VPJ`;
- `timesTrades`: ultimo negocio e agregados por `ULT`, `QUL`, `NEG`, `QTT` e `VOL`.

Na implementacao atual, `book` e `timesTrades` sao canais derivados do mesmo servidor `RTDTrading.RTDServer`. Eles preparam a arquitetura para book profundo e Times & Trades completo quando os respectivos RTDs/formulas estiverem disponiveis.

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

A secao `Ativos RTD` seleciona qual ativo aparece nos campos intraday, DOM, fluxo e setups. Snapshots de ativos diferentes ficam em caches separados no navegador, e os canais podem ser ajustados por ativo sem reiniciar o app.

## Aba DOM

A aba `DOM` e a primeira aba principal. Ela mostra:

- escada em tick size de 0,5;
- ultimo preco ao centro;
- bid/ask quando `OCP` e `OVD` chegam pelo RTD;
- volumes do book quando `VOC` e `VOV` chegam;
- tape recente dos movimentos tick a tick;
- pontos principais do motor quant quando o CSV ja foi carregado.

Os pontos marcados incluem abertura, maxima, minima, VWAP/MED, POC, VAH, VAL, desvios por abertura, desvios por POC e confluencias.

## Persistencia

SQLite e auxiliar. Se o provider SQLite falhar, o RTD e o WebSocket continuam rodando.

Tabelas:

- `snapshots`: ultimo estado consolidado salvo por intervalo.
- `minute_snapshots`: ultimo estado de cada minuto por ativo.
