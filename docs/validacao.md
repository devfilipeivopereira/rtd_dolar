# Validacao e Troubleshooting

## Validacao minima RTD

Depois de compilar no Visual Studio:

```text
ColetorProfitRTD.exe --probe
```

Esse modo assina apenas `WDOFUT_F_0 / VOL` por 30 segundos.

Resultado esperado:

```text
10:15:22.123 | WDOFUT_F_0 | VOL | 123456789
```

## Validacao completa

1. Abrir o Profit Pro e deixar conectado.
2. Executar `ColetorProfitRTD.exe`.
3. Abrir `http://localhost:5000`.
4. Abrir `Ativos`, cadastrar o ativo e carregar o CSV historico.
5. Confirmar status `RTD Conectado`.
6. Confirmar que a aba `DOM` mostra preco, tape e pontos principais.
7. Adicionar um ativo em `Ativos`, selecionar com `Ver` e ligar/desligar sem reiniciar o app.

## Endpoints

```text
GET http://localhost:5000/health
GET http://localhost:5000/snapshot
GET http://localhost:5000/flow
GET http://localhost:5000/signals
GET http://localhost:5000/assets
POST http://localhost:5000/assets
POST http://localhost:5000/assets/toggle
POST http://localhost:5000/assets/channels
POST http://localhost:5000/assets/delete
POST http://localhost:5000/assets/history
GET  http://localhost:5000/assets/history?asset=WDON26_G_0
DELETE http://localhost:5000/assets
WS  ws://localhost:5000/ws
```

Exemplos:

```json
POST /assets
{
  "asset": "WDON26_G_0",
  "enabled": true,
  "priceRtd": { "topic": "WDON26_G_0", "enabled": true },
  "bookRtd": { "topic": "BOOK0", "enabled": true, "depth": 50 },
  "timesRtd": { "topic": "T&T0", "enabled": true, "rows": 100 }
}

POST /assets/toggle
{ "asset": "WDON26_G_0", "enabled": false }

POST /assets/channels
{ "asset": "WDON26_G_0", "channels": ["price", "book"] }

POST /assets/delete
{ "asset": "WDON26_G_0" }
```

## x64 ou x86

Teste primeiro `x64`. Se o COM falhar com classe nao registrada, compile e rode em `x86`.

Sintomas comuns:

- `RTDTrading.RTDServer nao encontrado`;
- `Class not registered`;
- `InvalidCastException` no cast COM.

## Profit fechado

Com o Profit fechado:

- `/health` deve mostrar RTD desconectado;
- o HTML deve mostrar `Reconectando`;
- o app nao deve encerrar.

## Sem CSV

Sem CSV, a aba DOM ainda pode mostrar ticks RTD, bid/ask e tape. Pontos como POC, VAH, VAL, desvios e confluencias so aparecem depois do CSV.

## Multiativo

- `/assets` deve listar `enabled`, `subscribed` e `isDefault`.
- `/assets` deve listar `priceRtd`, `bookRtd`, `timesRtd`, `channels`, `fields` e `history` por ativo.
- Ativo desligado deve parar de receber novos snapshots.
- Ativo excluido deve sumir de `/assets` e da lista do dashboard.
- Ao selecionar outro ativo no dashboard, campos intraday, DOM, fluxo e setups devem mostrar somente aquele ativo.
- Order flow, delta e sinais nao devem misturar ativos diferentes.
- Ao desligar `book`, mensagens `bookDepth` deixam de atualizar depois da reassinatura.
- Ao desligar `timesTrades`, o tape real deixa de atualizar e a UI pode manter fallback derivado.
- CSV salvo deve voltar por `GET /assets/history?asset=...` depois de recarregar a pagina.

## SQLite

SQLite e auxiliar. Se o provider nao restaurar no NuGet ou se o banco falhar, o RTD e o WebSocket seguem funcionando.

## Build no ambiente Codex

Nesta maquina, a validacao foi feita com o `csc.exe` restaurado em `packages/Microsoft.Net.Compilers.Toolset.4.8.0`. Para uso normal, compile com Visual Studio 2022 ou Build Tools modernos.
