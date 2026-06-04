# Plano Implementado

## Entregue

- Solucao Visual Studio `ColetorProfitRTD.sln`.
- Projeto C# .NET Framework 4.8 com configuracoes AnyCPU, x64 e x86.
- Cliente RTD em thread STA dedicada.
- Controle multiativo com cadastro, liga/desliga e assinatura dinamica por ativo.
- Tela principal `Ativos` com cadastro guiado de `price`, `book` e `timesTrades`.
- Persistencia em `data/assets/assets.json` e CSV por ativo em `data/assets/{ativo}/history.csv`.
- Exclusao de RTD por ativo e controle de fontes `price`, `book` e `timesTrades`.
- Catalogo RTD com os campos colados pelo usuario.
- Snapshot consolidado com `rtd`, `intraday` e `book`.
- Servidor local `HttpListener` com `/`, `/health`, `/snapshot`, `/flow`, `/signals`, `/assets`, `/assets/toggle`, `/assets/channels`, `/assets/delete`, `/assets/history` e `/ws`.
- WebSocket com broadcast de snapshots, status, flow, signals, `bookDepth` e `timesTrades`.
- SQLite auxiliar para snapshots de 1 segundo e consolidado por minuto.
- HTML do `dolar-points` importado e adaptado com menu superior `Ativos`, `Panorama`, `DOM`, `Fluxo`, `Setups` e `Diagnostico`.

## Fluxo de validacao

1. Abrir Profit Pro.
2. Compilar x64 no Visual Studio.
3. Rodar o exe e abrir `http://localhost:5000`.
4. Se o RTD nao conectar, compilar x86.
5. Cadastrar ativo em `Ativos` e carregar CSV historico.
6. Confirmar que os campos intraday sao preenchidos por RTD.
7. Adicionar outro ativo em `Ativos`, testar `Ver`, `Ligado`, `Desligado`, fontes e `Excluir`.

## Build validado

O build foi validado com `csc.exe` do pacote `Microsoft.Net.Compilers.Toolset.4.8.0`. Para desenvolvimento normal, use Visual Studio 2022/Build Tools com restore NuGet.
