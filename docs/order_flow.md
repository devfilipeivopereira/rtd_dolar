# Tape Reading e Order Flow

## Escopo

A expansao de order flow tem duas camadas:

- metricas MVP ainda derivadas dos snapshots de preco/topo de book;
- tape e diagnostico passam a receber `timesTrades` real quando o RTD `T&Tn` estiver cadastrado e ligado.

Campos usados pelo motor derivado:

- `ULT`, `QUL`, `VOL`, `QTT`, `NEG`;
- `OCP`, `OVD`, `VOC`, `VOV`;
- `MED` como fallback de VWAP.

As metricas e sinais do MVP continuam derivados do ultimo preco, quantidade, volume acumulado e topo do book. O book multi-nivel e o Times & Trades real ja entram no WebSocket como mensagens separadas para a UI e para evolucao futura do motor.

No gerenciador de ativos:

- `price` alimenta snapshots e o motor derivado;
- `book` assina `BOOK0` por nivel e transmite `bookDepth`;
- `timesTrades` assina `T&T0` por linha e transmite `timesTrades`.

Quando `timesTrades` chega, a UI usa esse tape real. Sem ele, permanece o tape derivado.

Para manter agilidade sem repintar tabelas gigantes a cada campo RTD, o backend envia:

- `bookDepth` no maximo a cada 100 ms por ativo;
- `timesTrades` no maximo a cada 150 ms por ativo;
- `snapshot` e `flow` continuam nos throttles proprios.

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

Com varios ativos ligados, o `FlowProcessor` mantem `SnapshotCoalescer`, `FlowEngine`, trades, VWAP, profile e sinais separados por ativo. A mensagem `/flow` continua retornando o ultimo fluxo visto para compatibilidade, enquanto o WebSocket transmite cada `flow` com o respectivo `asset`. Ao excluir um ativo, o estado de fluxo dele tambem e removido da memoria.

## Endpoints

```text
GET /flow     -> ultima mensagem flow
GET /signals  -> sinais ativos/recentes
WS  /ws       -> snapshot, status, flow, signal, bookDepth e timesTrades
```

## Qualidade dos Dados

Valores possiveis:

- `unknown`;
- `topOfBookOnly`;
- `derivedTape`;
- `fullTimesAndTrades` reservado para o motor quando T&T real for usado nos calculos;
- `fullDepth` reservado para o motor quando book multi-nivel for usado nos calculos.

Scores sao limitados por qualidade:

- `topOfBookOnly`: maximo 78;
- `derivedTape`: maximo 85.

## UI

O dashboard usa o menu superior:

- `Cotacoes`: watchlist de ativos com ultimo preco, bid/ask, delta e status das fontes;
- `DOM`: escada de preco com pontos, book no nivel e sinais ativos;
- `Book`: tabelas dedicadas de compra e venda por nivel;
- `T&T`: Times & Trades real do RTD quando disponivel;
- `Fluxo`: delta, cumulative delta, imbalance, OFI, microbias, VWAP e tape real/derivado;
- `Setups`: sinais confirmados com score, direcao, preco e motivos;
- `Alertas`: alertas locais de preco por ativo;
- `Risco`: calculadora local de stop, alvo e contratos;
- `Historico`: CSV carregado e ticks recentes em memoria;
- `Sistema`: qualidade, eventos, bookDepth, Times & Trades, sinais e fila.

A aba `DOM` tambem marca sinais ativos como pontos de flow.
