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
4. Carregar CSV diario no dashboard.
5. Confirmar status `RTD Conectado`.
6. Confirmar que a aba `DOM` mostra preco, tape e pontos principais.

## Endpoints

```text
GET http://localhost:5000/health
GET http://localhost:5000/snapshot
WS  ws://localhost:5000/ws
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

## SQLite

SQLite e auxiliar. Se o provider nao restaurar no NuGet ou se o banco falhar, o RTD e o WebSocket seguem funcionando.

## Build no ambiente Codex

Nesta maquina foi encontrado apenas o MSBuild legado do .NET Framework, que nao entende `PackageReference`. Compile com Visual Studio 2022 ou Build Tools modernos.
