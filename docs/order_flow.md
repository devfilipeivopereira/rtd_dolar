# Tape Reading e Order Flow

## Escopo

A expansao de order flow usa os campos RTD ja disponiveis no Profit:

- `ULT`, `QUL`, `VOL`, `QTT`, `NEG`;
- `OCP`, `OVD`, `VOC`, `VOV`;
- `MED` como fallback de VWAP.

O MVP nao e Times & Trades completo nem book multi-nivel. As metricas e sinais sao derivados do ultimo preco, quantidade, volume acumulado e topo do book.

## Fluxo

```text
MarketSnapshot
  -> FlowProcessor background
  -> SnapshotCoalescer
  -> FlowEngine
  -> SetupDetector
  -> WebSocket type=flow/type=signal
```

O RTD continua rodando em thread STA. O `FlowProcessor` recebe snapshots em fila bounded/drop-old e usa coalescing para evitar interpretar updates campo-a-campo como negocios separados.

## Endpoints

```text
GET /flow     -> ultima mensagem flow
GET /signals  -> sinais ativos/recentes
WS  /ws       -> snapshot, status, flow e signal
```

## Qualidade dos Dados

Valores possiveis:

- `unknown`;
- `topOfBookOnly`;
- `derivedTape`;
- `fullTimesAndTrades` futuro;
- `fullDepth` futuro.

Scores sao limitados por qualidade:

- `topOfBookOnly`: maximo 78;
- `derivedTape`: maximo 85.

## UI

O dashboard tem tres abas novas:

- `Fluxo`: delta, cumulative delta, imbalance, OFI, microbias, VWAP e trades derivados;
- `Setups`: sinais confirmados com score, direcao, preco e motivos;
- `Debug Fluxo`: qualidade, eventos, trades derivados, sinais e fila.

A aba `DOM` tambem marca sinais ativos como pontos de flow.

