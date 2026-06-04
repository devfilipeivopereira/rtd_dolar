# Plano para Novo Projeto Nativo Low-Latency

## Objetivo

Criar um novo projeto Windows separado, mantendo o projeto atual intacto.

O projeto atual continua sendo:

```text
D:\OneDrive\Documentos\RTD_C#
```

Ele usa:

```text
Profit -> RTDTrading.RTDServer -> C# .NET Framework 4.8 -> HTTP/WebSocket -> HTML
```

O novo projeto sera a opcao 1, focada em menor delay:

```text
Profit -> RTDTrading.RTDServer -> C# .NET Framework 4.8 -> UI nativa Windows
```

Sem HTML, sem navegador e sem WebSocket no fluxo principal.

## Decisao Recomendada

Criar um app desktop nativo em C# com WPF.

Padrao recomendado:

- Linguagem: C#.
- Runtime inicial: .NET Framework 4.8.
- UI: WPF.
- Arquitetura: testar primeiro x64; se COM falhar, testar x86.
- RTD: reaproveitar a ponte COM ja validada no projeto atual.
- Banco: SQLite opcional e sempre em segundo plano.
- CSV: importar historico diario como no frontend atual.
- Calculos: portar o motor quant do JavaScript para C#.

Motivo para .NET Framework 4.8:

- O RTD COM ja foi validado nesse formato.
- A assinatura `IRtdServer` ja foi corrigida e funcionou com o Profit.
- Reduz risco no primeiro marco.

Motivo para WPF:

- Permite uma tela nativa mais rica que WinForms.
- Tem boa renderizacao para DOM, tape, tabelas e graficos customizados.
- Permite atualizar controles via `Dispatcher` com throttles diferentes.

WinForms tambem funciona, mas WPF e melhor para reconstruir o painel completo.

## Nome e Local Sugeridos

Criar fora do repo atual:

```text
D:\OneDrive\Documentos\RTD_Dolar_Nativo
```

Nome da solucao:

```text
RtdDolarNative.sln
```

Nome do projeto:

```text
RtdDolarNative
```

Repositorio Git novo sugerido:

```text
devfilipeivopereira/rtd_dolar_native
```

Nao misturar com o repo atual ate o MVP nativo estar funcionando.

## Regras de Performance

O objetivo e deixar o preco o mais proximo possivel do Profit.

Regras obrigatorias:

- O RTD roda em thread STA dedicada.
- A thread RTD nunca grava SQLite diretamente.
- A thread RTD nunca atualiza UI diretamente.
- A thread RTD nunca executa calculos pesados.
- A UI consome sempre o snapshot mais recente.
- Se chegar update mais rapido que a UI, descartar fila antiga e mostrar o ultimo estado.
- Preco, DOM e tape atualizam rapido.
- Grafico e calculos quant atualizam com throttle.
- Logs por tick ficam desligados por padrao.
- SQLite grava em lote ou por timer em segundo plano.

Timers recomendados:

| Fluxo | Intervalo inicial |
|---|---:|
| Poll RTD | 50 a 100 ms |
| UI preco/DOM/tape | 16 a 33 ms |
| Recalculo quant leve | 250 a 500 ms |
| Grafico completo | 500 a 1000 ms |
| SQLite snapshots | 1000 ms |
| Heartbeat RTD | 1000 ms |
| Reconnect | 5000 ms |

Observacao: o limite real pode ser o proprio `RTDTrading.RTDServer`. O app nao deve criar delay adicional relevante.

## Arquitetura Alvo

```text
RtdDolarNative.exe
  App.xaml / MainWindow.xaml
    -> PriceHeader
    -> NativeDomLadder
    -> TapePanel
    -> KeyLevelsPanel
    -> NativeChartPanel
    -> DiagnosticsPanel

  RtdWorker STA
    -> RTDTrading.RTDServer
    -> MarketState
    -> LatestSnapshotBuffer
    -> TickBuffer

  UiScheduler
    -> DispatcherTimer rapido para preco/DOM/tape
    -> DispatcherTimer medio para niveis
    -> DispatcherTimer lento para grafico

  QuantEngine
    -> CSV diario
    -> volatilidade 21/45/63
    -> POC proxy / VAH / VAL
    -> confluencias
    -> niveis para DOM e grafico

  BackgroundStorage
    -> SQLite opcional
    -> logs
    -> metricas de latencia
```

## Estrutura de Pastas

```text
RTD_Dolar_Nativo/
  RtdDolarNative.sln
  README.md
  docs/
    arquitetura.md
    campos_rtd.md
    validacao.md
    plano_execucao.md
  src/
    RtdDolarNative/
      App.config
      appsettings.json
      App.xaml
      App.xaml.cs
      MainWindow.xaml
      MainWindow.xaml.cs
      Config/
        AppConfig.cs
      Rtd/
        IRtdServer.cs
        IRTDUpdateEvent.cs
        RtdUpdateEvent.cs
        RtdClient.cs
        RtdTopic.cs
        RtdFieldCatalog.cs
      MarketData/
        MarketSnapshot.cs
        MarketState.cs
        ValueParser.cs
        TickEvent.cs
        BookState.cs
      LowLatency/
        LatestSnapshotBuffer.cs
        RingBuffer.cs
        UiUpdateScheduler.cs
        LatencyMetrics.cs
      Csv/
        DailyBar.cs
        DailyCsvParser.cs
      Quant/
        QuantResult.cs
        VolatilityEngine.cs
        VolumeProfileProxy.cs
        SupportResistanceEngine.cs
        AnchoredVwapEngine.cs
        PercentLevelEngine.cs
        ConfluenceEngine.cs
        KeyLevel.cs
      Dom/
        DomRow.cs
        DomLadderModel.cs
        DomLadderControl.xaml
        DomLadderControl.xaml.cs
      Charts/
        NativeChartControl.cs
        ChartLevel.cs
      Storage/
        SqliteSnapshotStore.cs
      Logging/
        Logger.cs
  tests/
    RtdDolarNative.Tests/
      ValueParserTests.cs
      DailyCsvParserTests.cs
      QuantParityTests.cs
```

## Codigo do Projeto Atual a Reaproveitar

Copiar/adaptar estes arquivos do projeto atual:

```text
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\Rtd\IRtdServer.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\Rtd\IRtdUpdateEvent.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\Rtd\RtdUpdateEvent.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\Rtd\RtdClient.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\Rtd\RtdTopic.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\Rtd\RtdFieldCatalog.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\MarketData\MarketSnapshot.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\MarketData\MarketState.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\MarketData\ValueParser.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\AppConfig.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\Logger.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\appsettings.json
```

Nao copiar para o fluxo principal:

```text
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\Web\
D:\OneDrive\Documentos\RTD_C#\src\dashboard\
```

Esses ficam apenas como referencia visual e logica.

## Assinatura COM Critica

Manter a assinatura que ja funcionou.

```csharp
[ComImport]
[Guid("EC0E6191-DB51-11D3-8F3E-00C04F3651B8")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IRtdServer
{
    [DispId(10)]
    int ServerStart(IRTDUpdateEvent callback);

    [DispId(11)]
    object ConnectData(
        int topicId,
        [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)] ref object[] strings,
        ref bool getNewValues);

    [DispId(12)]
    [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)]
    object[,] RefreshData(ref int topicCount);

    [DispId(13)]
    void DisconnectData(int topicId);

    [DispId(14)]
    int Heartbeat();

    [DispId(15)]
    void ServerTerminate();
}
```

No teste real, `ConnectData` funcionou com `object[]` zero-based.

## Campos RTD Padrao

Usar os mesmos campos do projeto atual:

```text
DAT
HOR
ULT
ABE
MAX
MIN
FEC
VAR
VARPTS
MED
NEG
QUL
QTT
VOL
OCP
OVD
VOC
VOV
AJU
AJA
VPJ
VEN
VAL
CAB
EST
```

Campos principais para baixa latencia visual:

| Campo | Uso |
|---|---|
| ULT | preco atual |
| HOR | hora do Profit |
| QUL | quantidade do ultimo negocio |
| OCP | oferta de compra |
| OVD | oferta de venda |
| VOC | volume na compra |
| VOV | volume na venda |
| ABE | abertura |
| MAX | maxima parcial |
| MIN | minima parcial |
| MED | media/proxy VWAP |
| VOL | volume acumulado |
| VPJ | volume projetado |

## Configuracao Inicial

Arquivo sugerido:

```text
src/RtdDolarNative/appsettings.json
```

Conteudo inicial:

```json
{
  "Rtd": {
    "ProgId": "RTDTrading.RTDServer",
    "Asset": "WDOFUT_F_0",
    "PollIntervalMs": 50,
    "ReconnectIntervalMs": 5000,
    "TickSize": 0.5,
    "Fields": [
      "DAT", "HOR", "ULT", "ABE", "MAX", "MIN", "FEC",
      "VAR", "VARPTS", "MED", "NEG", "QUL", "QTT", "VOL",
      "OCP", "OVD", "VOC", "VOV", "AJU", "AJA", "VPJ",
      "VEN", "VAL", "CAB", "EST"
    ]
  },
  "Ui": {
    "FastIntervalMs": 16,
    "QuantIntervalMs": 500,
    "ChartIntervalMs": 1000,
    "DomTicksEachSide": 100,
    "TapeCapacity": 500
  },
  "Storage": {
    "Enabled": false,
    "SnapshotIntervalMs": 1000,
    "ConnectionString": "Data Source=data/marketdata.sqlite;Version=3;"
  },
  "Diagnostics": {
    "LogPath": "logs/rtd-dolar-native.log",
    "LogEveryTick": false
  }
}
```

## Modelo de Dados

### MarketSnapshot

Reaproveitar o modelo atual, mas remover dependencia de JSON/web.

Campos derivados importantes:

```text
Asset
Status
LocalTimestamp
DataProfit
HoraProfit
Ultimo
Abertura
Maxima
Minima
FechamentoAnterior
Media
Volume
Quantidade
Negocios
OfertaCompra
OfertaVenda
VolumeOfertaCompra
VolumeOfertaVenda
VolumeProjetado
Raw
Rtd
```

### TickEvent

Criar:

```csharp
public sealed class TickEvent
{
    public DateTimeOffset LocalTimestamp { get; set; }
    public string ProfitTime { get; set; }
    public decimal Price { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Volume { get; set; }
    public decimal Delta { get; set; }
    public string Side { get; set; } // Compra, Venda, Subiu, Caiu, Inicial
    public decimal? Bid { get; set; }
    public decimal? Ask { get; set; }
}
```

Gerar tick quando `ULT` mudar pelo menos `tickSize / 2`.

### KeyLevel

Criar:

```csharp
public sealed class KeyLevel
{
    public decimal Price { get; set; }
    public string Label { get; set; }
    public string Type { get; set; } // Atual, Valor, Suporte, Resistencia, Magneto
    public string Source { get; set; }
    public double Score { get; set; }
}
```

O DOM deve aceitar todos os `KeyLevel` existentes.

## UI Nativa Esperada

### Header

Mostrar:

- ativo;
- status RTD: conectado, reconectando, sem Profit, manual;
- arquitetura do processo: x64/x86;
- hora do Profit;
- ultimo update local;
- idade do snapshot em ms;
- botao conectar/desconectar;
- botao modo manual.

### DOM Nativo

Colunas:

```text
AskVol | Preco | BidVol | Marcacoes
```

Comportamento:

- centralizar no preco atual por padrao;
- permitir rolagem;
- mostrar pelo menos 200 ticks ao redor do preco atual;
- expandir se houver niveis fora da faixa inicial;
- marcar todos os pontos calculados no mesmo preco;
- nao limitar quantidade de tags por preco;
- destacar ultimo preco;
- destacar bid e ask;
- mostrar volumes `VOC` e `VOV`;
- manter renderizacao leve.

Implementacao recomendada:

- Nao recriar milhares de controles por tick.
- Usar lista virtualizada ou controle customizado.
- Manter rows em memoria e atualizar propriedades.
- Usar `ObservableCollection<DomRow>` apenas se a frequencia ficar aceitavel.
- Se a UI pesar, trocar para `DrawingVisual`/controle customizado.

### Tape

Mostrar os ultimos 500 ticks:

```text
Hora | Preco | Delta | Lado | QUL
```

Manter ring buffer.

### Niveis Principais

Mostrar tabela lateral:

```text
Preco | Distancia | Tipo | Score | Fonte | Evidencia
```

Nao cortar artificialmente. Pode ordenar por distancia do preco atual.

### Grafico Nativo

Reconstruir o grafico do HTML em nativo.

MVP:

- candles diarios do CSV;
- candle atual;
- linhas horizontais para niveis;
- preco atual;
- VWAP/MED;
- POC;
- VAH/VAL;
- sigma;
- abertura e desvios;
- confluencias.

Para performance, preferir desenho customizado em WPF:

- `FrameworkElement.OnRender`;
- `DrawingContext`;
- cache de geometria;
- redesenho com throttle.

Evitar grafico pesado no primeiro MVP.

## CSV Diario

Portar o parser do HTML.

Suportar:

- delimitador `;`;
- delimitador `,`;
- tab;
- UTF-8;
- Windows-1252;
- cabecalho em portugues ou ingles.

Cabecalhos aceitos:

| Campo | Cabecalhos |
|---|---|
| ativo | ativo, symbol, ticker |
| data | data, date |
| abertura | abertura, open |
| maxima | max, high, maximo |
| minima | min, low, minimo |
| fechamento | fech, close, ultimo |
| volume | volume, vol |
| quantidade | quant, qty, trades |

Formatos sem cabecalho:

```text
Data;Abertura;Maxima;Minima;Fechamento;Volume;Quantidade
Ativo;Data;Abertura;Maxima;Minima;Fechamento;Volume;Quantidade
```

Validacao:

- minimo 21 pregoes validos;
- deduplicar por data;
- ordenar por data crescente;
- aceitar numeros pt-BR como `5.432,50`;
- aceitar numeros grandes como `142.390.903.037,92`.

## Motor Quant a Portar do HTML

Fonte de referencia:

```text
D:\OneDrive\Documentos\RTD_C#\src\dashboard\index.html
```

Funcoes/conceitos a portar para C#:

- `parseDailyCsv`
- `calcGarmanKlass`
- `calcParkinson`
- `calcRogersSatchell`
- `calcYangZhang`
- `calcCloseToClose`
- `calcATR`
- `volumeProfileProxy`
- `detectPivots`
- `detectRejections`
- `detectRoundLevels`
- `supportResistanceEngine`
- `anchoredVWAPs`
- `varianceRatio`
- `linearSlopeNormalized`
- `detectRegime`
- `openingDeviationLevels`
- `referenceDeviationLevels`
- `percentVariationMaps`
- `percentInterestLevels`
- `mergeInterestLevels`
- `buildResult`

Janelas:

```text
21
45
63
```

Niveis que precisam ir para DOM e grafico:

- preco atual;
- abertura;
- maxima atual;
- minima atual;
- VWAP/MED;
- POC proxy;
- VAH;
- VAL;
- HVN;
- LVN;
- sigmas;
- abertura +/- desvios;
- POC +/- desvios;
- variacoes percentuais de D-1;
- variacoes percentuais da abertura;
- variacoes percentuais do POC;
- niveis do dia anterior;
- AVWAPs;
- suportes/resistencias;
- confluencias.

Regra importante: o DOM deve mostrar tudo que existir, inclusive multiplas marcacoes no mesmo preco.

## Fluxo de Baixa Latencia

### RTD Thread

Responsabilidades:

1. Criar instancia COM `RTDTrading.RTDServer`.
2. Chamar `ServerStart`.
3. Assinar campos com `ConnectData`.
4. Aguardar `UpdateNotify` ou fazer poll curto.
5. Chamar `RefreshData`.
6. Atualizar `MarketState`.
7. Publicar `LatestSnapshot`.
8. Gerar `TickEvent` quando `ULT` mudar.

Nao fazer:

- atualizar UI;
- gravar banco;
- recalcular grafico;
- logar cada update.

### Latest Snapshot Buffer

Usar ideia `latest wins`.

Se chegarem 20 updates antes da UI pintar, a UI pega so o mais recente.

Pode ser implementado com:

- campo privado protegido por `lock`;
- `Interlocked.Exchange` com referencia imutavel;
- versao incremental `long`.

### UI Scheduler

Usar `DispatcherTimer`.

Timer rapido:

- le snapshot mais recente;
- atualiza preco;
- atualiza bid/ask;
- atualiza DOM;
- atualiza tape.

Timer medio:

- se CSV carregado e intraday mudou, recalcula niveis.

Timer lento:

- redesenha grafico.

## SQLite e Logs

SQLite e opcional no MVP nativo.

Se usar:

- gravar snapshots consolidados a cada 1 segundo;
- gravar buckets por minuto;
- nunca bloquear RTD;
- usar fila separada;
- se falhar, app continua rodando.

Logs:

- startup;
- arquitetura;
- ServerStart;
- ConnectData por campo;
- status conectado/desconectado;
- erros COM;
- reconnect;
- metricas agregadas.

Nao logar cada tick por padrao.

## Ordem de Implementacao

### Marco 0 - Criacao do Projeto

1. Criar pasta `D:\OneDrive\Documentos\RTD_Dolar_Nativo`.
2. Criar `RtdDolarNative.sln`.
3. Criar projeto WPF .NET Framework 4.8.
4. Configurar plataformas `x64` e `x86`.
5. Criar README inicial.
6. Criar `appsettings.json`.
7. Criar `.gitignore`.

### Marco 1 - Prova RTD Nativa

1. Copiar interfaces COM do projeto atual.
2. Criar `RtdProbeService`.
3. Assinar apenas `WDOFUT_F_0` + `ULT` + `VOL`.
4. Mostrar numa janela simples:
   - status;
   - ultimo preco;
   - volume;
   - hora.
5. Validar x64.
6. Se falhar, validar x86.

Criterio de aceite:

- Com Profit aberto, preco muda na janela.
- Com Profit fechado, app fica vivo e mostra reconectando.

### Marco 2 - Engine RTD Completa

1. Assinar todos os campos padrao.
2. Criar `MarketState`.
3. Criar `LatestSnapshotBuffer`.
4. Criar `TickEvent`.
5. Criar ring buffer de tape.
6. Criar metricas de latencia.

Criterio de aceite:

- `ULT`, `OCP`, `OVD`, `VOC`, `VOV`, `ABE`, `MAX`, `MIN`, `MED`, `VOL` aparecem na memoria e UI.

### Marco 3 - DOM Nativo

1. Criar `DomRow`.
2. Criar `DomLadderModel`.
3. Mostrar 200 ticks ao redor do atual.
4. Permitir rolagem.
5. Destacar ultimo preco.
6. Destacar bid/ask.
7. Mostrar volumes do book.
8. Mostrar marcacoes basicas:
   - ultimo;
   - abertura;
   - maxima;
   - minima;
   - VWAP/MED.

Criterio de aceite:

- Ao mudar `ULT`, a linha atual muda sem travar.
- Tape registra os movimentos.

### Marco 4 - CSV Diario

1. Portar parser CSV.
2. Criar botao de carregar CSV.
3. Mostrar contagem de pregoes.
4. Validar 21/45/63.
5. Guardar bars em memoria.

Criterio de aceite:

- Mesmo CSV aceito pelo HTML deve ser aceito no app nativo.

### Marco 5 - Motor Quant em C#

1. Portar volatilidades.
2. Portar ATR.
3. Portar volume profile proxy.
4. Portar POC/VAH/VAL/HVN/LVN.
5. Portar suportes/resistencias.
6. Portar AVWAPs.
7. Portar variacoes percentuais.
8. Portar confluencias.
9. Gerar `List<KeyLevel>`.

Criterio de aceite:

- Com os mesmos valores de CSV + intraday, principais niveis batem com o HTML dentro de 0,5 ponto.

### Marco 6 - DOM com Todos os Pontos

1. Alimentar DOM com todos os `KeyLevel`.
2. Permitir multiplas tags no mesmo preco.
3. Expandir faixa se algum nivel estiver fora dos 200 ticks iniciais.
4. Mostrar tooltip/detalhe do ponto.

Criterio de aceite:

- Todos os pontos do grafico tambem aparecem no DOM.
- Nenhum corte artificial por quantidade de tags.

### Marco 7 - Grafico Nativo

1. Criar controle de grafico.
2. Desenhar candles diarios.
3. Desenhar candle atual.
4. Desenhar linhas dos niveis.
5. Adicionar zoom/pan simples.
6. Adicionar labels laterais.

Criterio de aceite:

- O grafico mostra os mesmos grupos do HTML.
- Redesenho pesado nao ocorre a cada tick.

### Marco 8 - Persistencia e Diagnostico

1. Adicionar SQLite opcional.
2. Adicionar tela de diagnostico.
3. Mostrar:
   - arquitetura;
   - status COM;
   - ultimo heartbeat;
   - updates por segundo;
   - idade do snapshot;
   - erros recentes.

Criterio de aceite:

- Falha no SQLite nao derruba RTD.

### Marco 9 - Empacotamento

1. Gerar build x64.
2. Gerar build x86.
3. Criar pasta `dist`.
4. Incluir `appsettings.json`.
5. Incluir README de uso.
6. Criar zip.

Criterio de aceite:

- Usuario abre o `.exe` e ve o app sem abrir navegador.

## Testes Obrigatorios

### RTD

- Profit fechado: app mostra reconectando e nao fecha.
- Profit aberto: `ServerStart` retorna `1`.
- x64: testar primeiro.
- x86: testar se x64 falhar.
- `ULT` muda em tela sem Excel.
- `VOL` chega sem Excel.
- `OCP/OVD/VOC/VOV` aparecem quando o Profit fornece.

### Parsing

- `5.432,50` vira `5432.50`.
- `142.390.903.037,92` vira `142390903037.92`.
- campo vazio vira `null/NaN`, nao crash.
- CSV UTF-8 funciona.
- CSV Windows-1252 funciona.
- CSV com `;`, `,` e tab funciona.

### UI

- Preco nao fica visivelmente atrasado em relacao ao Profit.
- UI nao trava com updates frequentes.
- Scroll do DOM funciona.
- Tape nao cresce infinito.
- DOM atualiza sem recriar tudo pesadamente.

### Quant

- Com CSV carregado, gera 21/45/63.
- POC/VAH/VAL aparecem.
- Desvios de abertura aparecem.
- Desvios de POC aparecem.
- Confluencias aparecem.
- Todos os niveis do grafico aparecem tambem no DOM.

### Latencia

Medir:

- hora de recebimento local do RTD;
- hora de pintura UI;
- updates por segundo;
- skips/coalescencias.

Meta inicial:

- Sem backlog.
- UI responsiva.
- Preco visual atualizado dentro do menor intervalo que o RTD entregar.

## Riscos e Mitigacoes

### RTD COM x64/x86

Risco:

- RTD registrado so em uma arquitetura.

Mitigacao:

- manter builds x64 e x86;
- testar x64 primeiro;
- registrar arquitetura funcional no README.

### InvalidCastException no COM

Risco:

- assinatura errada de `ConnectData` ou `RefreshData`.

Mitigacao:

- reutilizar assinatura ja validada;
- manter `object[]` zero-based;
- manter `SAFEARRAY(VARIANT)`.

### UI nativa travar

Risco:

- atualizar controles demais por tick.

Mitigacao:

- latest-wins;
- throttles;
- virtualizacao;
- desenho customizado;
- nao fazer calculo pesado no tick.

### Paridade com HTML

Risco:

- calculos portados para C# divergirem do JS.

Mitigacao:

- criar testes com o mesmo CSV e mesmo snapshot;
- comparar principais niveis com tolerancia de 0,5 ponto;
- portar uma familia de calculo por vez.

## O Que Nao Fazer no Novo Projeto

- Nao depender do navegador.
- Nao depender do HTML.
- Nao depender de WebSocket para a UI principal.
- Nao mexer no projeto atual durante o MVP.
- Somente analise; sem envio ao Profit.
- Nao emitir recomendacao automatica de compra/venda.
- Nao logar cada tick em disco.
- Nao gravar SQLite no caminho quente do RTD.
- Nao recalcular grafico completo a cada tick.

## Prompt Para Abrir Nova Sessao

Cole isto em uma nova sessao do Codex:

```text
Quero criar um novo projeto Windows nativo low-latency para RTD do Profit, mantendo intacto o projeto atual em:

D:\OneDrive\Documentos\RTD_C#

Leia primeiro este plano:

D:\OneDrive\Documentos\RTD_C#\docs\plano_novo_projeto_nativo_low_latency.md

Objetivo do novo projeto:

- criar uma nova pasta D:\OneDrive\Documentos\RTD_Dolar_Nativo;
- criar uma solucao WPF C# .NET Framework 4.8 chamada RtdDolarNative;
- usar RTDTrading.RTDServer sem Excel;
- reaproveitar a ponte COM ja validada do projeto atual;
- nao usar HTML/WebSocket/navegador como UI principal;
- criar UI nativa com preco, DOM, tape, niveis e grafico;
- priorizar menor delay possivel no preco;
- manter o projeto atual funcionando sem alteracoes.

Comece pelo Marco 0 e Marco 1:

1. criar a estrutura do novo projeto;
2. copiar/adaptar as interfaces COM e parser de valores;
3. criar uma prova RTD nativa assinando WDOFUT_F_0 com ULT e VOL;
4. mostrar ultimo preco e volume numa janela WPF;
5. preparar builds x64 e x86;
6. documentar como rodar e validar.

Use como referencia os arquivos:

D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\Rtd\IRtdServer.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\Rtd\IRtdUpdateEvent.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\Rtd\RtdUpdateEvent.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\Rtd\RtdClient.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\MarketData\ValueParser.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\MarketData\MarketSnapshot.cs
D:\OneDrive\Documentos\RTD_C#\src\ColetorProfitRTD\appsettings.json

Importante:

- a assinatura COM de IRtdServer precisa usar SAFEARRAY(VARIANT) como no projeto atual;
- ConnectData com object[] zero-based ja funcionou;
- RTD deve rodar em thread STA dedicada;
- UI deve usar latest snapshot, sem fila acumulando;
- SQLite e logs nao podem bloquear RTD;
- nao altere nem quebre o projeto atual.
```

## Checklist de Conclusao do MVP Nativo

- [ ] Novo projeto criado em pasta separada.
- [ ] Projeto atual permanece intacto.
- [ ] WPF abre janela propria.
- [ ] RTD conecta no Profit sem Excel.
- [ ] x64 testado.
- [ ] x86 testado se necessario.
- [ ] `ULT` aparece em tempo real.
- [ ] `VOL` aparece em tempo real.
- [ ] App sobrevive com Profit fechado.
- [ ] Thread RTD e UI ficam separadas.
- [ ] Nenhum IO bloqueia o caminho quente.
- [ ] README do novo projeto explica uso.
