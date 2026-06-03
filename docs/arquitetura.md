# Arquitetura

## Objetivo

O projeto transforma o RTD do Profit em uma fonte local de dados em tempo real para uma interface HTML. O Excel nao entra no fluxo: o aplicativo C# assume o papel de cliente RTD.

```text
Profit Pro
  -> RTDTrading.RTDServer
  -> RtdClient C# em thread STA
  -> MarketSnapshot
  -> WebSocket ws://localhost:5000/ws
  -> Dashboard HTML
```

## Backend

O backend fica em `src/ColetorProfitRTD`.

- `Rtd/`: interfaces COM, catalogo de campos e cliente RTD.
- `MarketData/`: parsing pt-BR e snapshot consolidado.
- `Web/`: `HttpListener`, arquivos estaticos, `/health`, `/snapshot` e `/ws`.
- `Storage/`: SQLite auxiliar para snapshots e buckets por minuto.

O RTD roda em uma thread STA dedicada. Isso evita misturar chamadas COM com o loop HTTP/WebSocket.

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
