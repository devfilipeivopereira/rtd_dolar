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
4. Confirmar que `Painel` abre como entrada operacional.
5. Abrir `Ativos`, cadastrar o ativo e carregar o CSV historico.
6. Confirmar status `RTD Conectado`.
7. Abrir `Painel` e confirmar checklist, atalhos, setups, oportunidades e alertas.
8. Abrir `Mesa` e confirmar DOM compacto, book resumido, tape, fluxo, setups, niveis proximos e acoes de analise.
9. Abrir `Monitor` e confirmar watchlist compacta, estado do ativo, setups, tape, oportunidades e alertas.
10. Confirmar que os botoes de `Monitor` abrem `DOM`, `Book` e `Oportunidades` do ativo correto.
11. Abrir `Cotacoes` e confirmar que a linha do ativo mostra ultimo preco, bid/ask, delta, Book, Times e fontes `P/B/T`.
12. Confirmar que os botoes de `Cotacoes` abrem `Grafico`, `DOM`, `Book`, `T&T` e `Oportunidades` do ativo correto.
13. Confirmar que a aba `DOM` mostra preco, tape e pontos principais.
14. Adicionar um ativo em `Ativos`, selecionar com `Ver` e ligar/desligar sem reiniciar o app.
15. Abrir `Book` e confirmar 50 niveis quando `BOOK0` estiver ligado.
16. Abrir `T&T` e confirmar ate 100 linhas quando `T&T0` estiver ligado.
17. Abrir `Oportunidades`, salvar uma ideia observacional e confirmar que ela permanece local.
18. Abrir `Alertas`, `Risco`, `Historico`, `Ajustes`, `Conexoes` e `Sistema` e confirmar que cada tela mostra apenas sua funcionalidade.
19. Em `Conexoes`, confirmar polling de `/health`, arquitetura, Profit RTD, WebSocket e fontes `Preco`, `Book` e `Times` por ativo.
20. Em `Ajustes`, mudar niveis do DOM e intervalo de renderizacao, salvar, recarregar a pagina e confirmar que os valores persistem.
21. Confirmar hotbar por clique e atalhos `Alt+1` a `Alt+9`.
22. Confirmar que os atalhos nao disparam quando o foco esta em campos de texto, select ou textarea.
23. Confirmar `Ctrl+K`, busca de telas e ativos, navegacao por setas, `Enter` para executar e `Esc` para fechar.
24. Confirmar que `Latencia WS` e `Msg/s` aparecem na faixa superior e no `Sistema` quando chegam mensagens do WebSocket.

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
- A faixa superior deve trocar junto com o ativo selecionado e mostrar ultimo preco, bid/ask, Book, Times, delta, latencia WebSocket local, mensagens por segundo e CSV.
- `Painel` deve ser a entrada operacional e refletir o ativo selecionado sem misturar dados de outro ativo.
- `Monitor` deve ser a mesa de acompanhamento ao vivo: watchlist compacta, estado do ativo, setups, tape, oportunidades e alertas, sem campos de cadastro.
- `Cotacoes` deve separar monitoramento de cadastro: a tela mostra status e atalhos, mas a edicao continua em `Ativos`.
- `Mesa` deve concentrar DOM compacto, book resumido, tape, fluxo, setups, niveis proximos e acoes de analise.
- `Oportunidades` deve salvar ideias em localStorage, calcular R/R e mudar status com preco RTD sem comandos operacionais ao Profit.
- `Ajustes` deve persistir em `wdo-ui-settings` e aplicar tamanho do tick, niveis do DOM, intervalo de renderizacao, limite de trades/sinais e valor por ponto padrao.
- `Conexoes` deve consultar `/health` periodicamente e separar status do feed local do debug de fluxo.
- A hotbar deve espelhar a aba ativa e permitir troca rapida para Monitor, DOM, Book, T&T, Fluxo, Oportunidades, Ativos, Conexoes e Sistema.
- A paleta `Ctrl+K` deve buscar telas e ativos cadastrados sem depender de recarregar a pagina.
- `Book` e `T&T` devem atualizar sem travar a pagina mesmo com muitos campos RTD, respeitando coalescing do backend.
- Campos intraday devem ser preenchidos a cada snapshot; DOM, `Mesa`, `Painel`, `Monitor`, `Cotacoes` e `Historico` podem atualizar em lote curto configuravel para manter a UI responsiva.
- `Latencia WS` deve ser tratada como diagnostico backend local -> navegador, nao como latencia de bolsa ou Profit.

## SQLite

SQLite e auxiliar. Se o provider nao restaurar no NuGet ou se o banco falhar, o RTD e o WebSocket seguem funcionando.

## Build no ambiente Codex

Nesta maquina, a validacao foi feita com o `csc.exe` restaurado em `packages/Microsoft.Net.Compilers.Toolset.4.8.0`. Para uso normal, compile com Visual Studio 2022 ou Build Tools modernos.

## Design QA

Para validar a direcao visual Industrial do dashboard:

```text
node tools/validate-dashboard-design.js
```

Resultado esperado:

```text
Dashboard design tokens OK
```

Esse check falha se o dashboard reintroduzir sombras, gradientes, filtros decorativos ou fontes fora da familia mono.

## Navegacao

No navegador, confirme:

1. Os grupos superiores `Inicio`, `Cadastro`, `Mercado`, `Fluxo`, `Controle` e `Sistema` trocam a hotbar contextual.
2. Ao voltar para um grupo, a hotbar reabre a ultima tela usada naquele grupo.
3. `Buscar`, `Ctrl+K`, setas, `Enter` e `Esc` continuam funcionando.
4. `Alt+1` a `Alt+9` continuam abrindo as telas frequentes quando o foco nao esta em campo de texto.
