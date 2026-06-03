# ColetorProfitRTD

Aplicacao local Windows para ler RTD do Profit Pro sem Excel e alimentar o HTML do `dolar-points` em tempo real.

Repositorio alvo: https://github.com/devfilipeivopereira/rtd_dolar

## Documentacao

- [Arquitetura](docs/arquitetura.md)
- [Campos RTD](docs/campos_rtd.md)
- [Validacao e troubleshooting](docs/validacao.md)
- [Plano implementado](docs/plano_implementado.md)
- [Plano do novo projeto nativo low-latency](docs/plano_novo_projeto_nativo_low_latency.md)

## Arquitetura

```text
Profit Pro conectado
  -> RTDTrading.RTDServer
  -> Coletor C# .NET Framework 4.8
  -> http://localhost:5000
  -> WDO_Reversal_Engine.html adaptado com RTD Live
```

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

Para uma prova minima sem dashboard:

```text
ColetorProfitRTD.exe --probe
```

Esse modo assina apenas `WDOFUT_F_0 / VOL` por 30 segundos.

Endpoints:

```text
GET /health
GET /snapshot
WS  /ws
```

## Configuracao

Edite `src/ColetorProfitRTD/appsettings.json`:

- `Rtd.Asset`: ativo RTD, padrao `WDOFUT_F_0`
- `Rtd.Fields`: campos assinados no RTD
- `Web.HttpPort`: porta HTTP/WebSocket
- `Storage.Enabled`: liga/desliga SQLite auxiliar

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
6. SQLite: confirmar criacao de `data/marketdata.sqlite` quando o provider for restaurado pelo NuGet.

## Observacao de ambiente

Neste workspace nao havia SDK .NET nem MSBuild moderno disponivel para compilar localmente. O MSBuild legado do .NET Framework foi encontrado, mas ele nao suporta `PackageReference`. A solucao foi criada para Visual Studio/.NET Framework 4.8 e deve ser validada no Visual Studio com restore NuGet.
