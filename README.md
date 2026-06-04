# ColetorProfitRTD

Aplicacao local Windows para ler RTD do Profit Pro sem Excel e alimentar o HTML do `dolar-points` em tempo real.

Repositorio alvo: https://github.com/devfilipeivopereira/rtd_dolar

## Documentacao

- [Arquitetura](docs/arquitetura.md)
- [Campos RTD](docs/campos_rtd.md)
- [Validacao e troubleshooting](docs/validacao.md)
- [Plano implementado](docs/plano_implementado.md)
- [Plano do novo projeto nativo low-latency](docs/plano_novo_projeto_nativo_low_latency.md)
- [Tape reading e order flow](docs/order_flow.md)

## Arquitetura

```text
Profit Pro conectado
  -> RTDTrading.RTDServer
  -> Coletor C# .NET Framework 4.8
  -> http://localhost:5000
  -> WDO_Reversal_Engine.html adaptado com RTD Live
```

O coletor pode assinar varios ativos RTD ao mesmo tempo. No dashboard, a secao `Ativos RTD` permite adicionar um simbolo, escolher qual ativo a tela esta exibindo, ligar/desligar, excluir e escolher quais canais RTD ficam ativos para cada simbolo:

- `Cotacao`: preco, abertura, maxima, minima, media/VWAP proxy e metadados;
- `Book`: oferta de compra/venda, volume na melhor compra/venda e volume projetado;
- `Times`: ultimo preco, quantidade do ultimo negocio, negocios, quantidade e volume.

Nesta fase, `Book` e `Times` usam os campos RTD disponiveis no Profit (`OCP`, `OVD`, `VOC`, `VOV`, `QUL`, `NEG`, `QTT`, `VOL`). Isso alimenta topo de book e tape derivado. Book multi-nivel e Times & Trades completo podem ser conectados depois se o Profit expuser formulas/servidores RTD especificos.

O historico diario continua sendo carregado por CSV no navegador. O RTD preenche os campos intraday usados pelo motor quant:

- Abertura: `ABE`
- Maxima parcial: `MAX`
- Minima parcial: `MIN`
- Preco atual: `ULT`
- VWAP/ancora opcional: `MED`
- Volume acumulado: `VOL`

A primeira aba principal do dashboard e `DOM`. Ela mostra:

- escada de preco em ticks de 0,5 ponto ao redor do ultimo preco;
- tape recente com movimento tick a tick;
- bid/ask do RTD quando `OCP`, `OVD`, `VOC` e `VOV` estiverem disponiveis;
- marcacoes de pontos principais como abertura, maxima, minima, VWAP/MED, POC, VAH/VAL, desvios e confluencias.

## Build

Prerequisitos:

- Windows 10/11
- Visual Studio 2022 ou Build Tools
- .NET Framework 4.8 Developer Pack
- Profit Pro instalado, aberto e logado

Abra `ColetorProfitRTD.sln` no Visual Studio e compile primeiro em `x64`.

Se aparecer `Class not registered` ou `RTDTrading.RTDServer nao encontrado`, compile em `x86` e teste novamente. A arquitetura precisa bater com o registro COM do RTD do Profit.

Use o MSBuild do Visual Studio 2022/Build Tools. O MSBuild antigo de `C:\Windows\Microsoft.NET\Framework*\v4.0.30319` nao entende `PackageReference` e falha antes da compilacao.

## Execucao

1. Abra o Profit Pro e deixe conectado.
2. Execute `ColetorProfitRTD.exe`.
3. Abra `http://localhost:5000`.
4. Carregue o CSV diario no HTML.
5. Deixe o modo `RTD Live` ativo para preencher o intraday automaticamente.
6. Em `Ativos RTD`, adicione novos simbolos, clique em `Ver` para selecionar o ativo da tela, use `Ligado/Desligado` para controlar a assinatura RTD daquele ativo, marque/desmarque canais e use `Excluir` para remover um RTD da lista.

Para uma prova minima sem dashboard:

```text
ColetorProfitRTD.exe --probe
```

Esse modo assina apenas `WDOFUT_F_0 / VOL` por 30 segundos.

Endpoints:

```text
GET /health
GET /snapshot
GET /flow
GET /signals
GET /assets
POST /assets
POST /assets/toggle
POST /assets/channels
POST /assets/delete
DELETE /assets
WS  /ws
```

## Configuracao

Edite `src/ColetorProfitRTD/appsettings.json`:

- `Rtd.Asset`: ativo RTD, padrao `WDOFUT_F_0`
- `Rtd.Assets`: ativos cadastrados ao iniciar o aplicativo
- `Rtd.ActiveAssets`: ativos que ja iniciam ligados
- `Rtd.AssetChannels`: canais habilitados por ativo
- `Rtd.ChannelFields`: campos RTD usados em cada canal
- `Rtd.Fields`: campos assinados no RTD
- `Web.HttpPort`: porta HTTP/WebSocket
- `Storage.Enabled`: liga/desliga SQLite auxiliar

Exemplo para iniciar com dois ativos cadastrados, mas apenas WDO ligado:

```json
"Rtd": {
  "Asset": "WDOFUT_F_0",
  "Assets": ["WDOFUT_F_0", "WINFUT_F_0"],
  "ActiveAssets": ["WDOFUT_F_0"],
  "AssetChannels": {
    "WDOFUT_F_0": ["quote", "book", "timesTrades"],
    "WINFUT_F_0": ["quote", "book"]
  }
}
```

O catalogo de campos conhecidos fica em `src/ColetorProfitRTD/Rtd/RtdFieldCatalog.cs`.

## Estrutura

```text
ColetorProfitRTD.sln
src/
  ColetorProfitRTD/     backend C# RTD, WebSocket, HTTP e SQLite
  dashboard/            HTML do dolar-points adaptado com RTD Live e DOM
docs/                   documentacao operacional
data/                   SQLite em runtime
logs/                   logs em runtime
```

## Testes manuais essenciais

1. Profit fechado: `/health` deve mostrar RTD desconectado e o HTML deve exibir reconexao.
2. Profit aberto: logs devem mostrar `ServerStart retornou 1` e campos assinados.
3. x64/x86: testar ambas se o COM nao registrar na primeira arquitetura.
4. CSV + RTD: carregar CSV e confirmar que o RTD atualiza os campos intraday.
5. Manual: desligar `RTD Live` e editar os campos manualmente.
6. Multiativo: adicionar um novo ativo em `Ativos RTD`, ligar/desligar, trocar canais, excluir e confirmar `/assets`.
7. SQLite: confirmar criacao de `data/marketdata.sqlite` quando o provider for restaurado pelo NuGet.
