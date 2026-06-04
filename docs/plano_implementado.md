# Plano Implementado

## Entregue

- Solucao Visual Studio `ColetorProfitRTD.sln`.
- Projeto C# .NET Framework 4.8 com configuracoes AnyCPU, x64 e x86.
- Cliente RTD em thread STA dedicada.
- Controle multiativo com cadastro, liga/desliga e assinatura dinamica por ativo.
- Exclusao de RTD por ativo e canais `quote`, `book` e `timesTrades`.
- Catalogo RTD com os campos colados pelo usuario.
- Snapshot consolidado com `rtd`, `intraday` e `book`.
- Servidor local `HttpListener` com `/`, `/health`, `/snapshot`, `/flow`, `/signals`, `/assets`, `/assets/toggle`, `/assets/channels`, `/assets/delete` e `/ws`.
- WebSocket com broadcast de snapshots, status, flow e signals.
- SQLite auxiliar para snapshots de 1 segundo e consolidado por minuto.
- HTML do `dolar-points` importado e adaptado com modo `RTD Live`, DOM, fluxo, setups e controle de ativos.

## Fluxo de validacao

1. Abrir Profit Pro.
2. Compilar x64 no Visual Studio.
3. Rodar o exe e abrir `http://localhost:5000`.
4. Se o RTD nao conectar, compilar x86.
5. Carregar CSV diario no HTML.
6. Confirmar que os campos intraday sao preenchidos por RTD.
7. Adicionar outro ativo em `Ativos RTD`, testar `Ver`, `Ligado`, `Desligado`, canais e `Excluir`.

## Build validado

O build foi validado com `csc.exe` do pacote `Microsoft.Net.Compilers.Toolset.4.8.0`. Para desenvolvimento normal, use Visual Studio 2022/Build Tools com restore NuGet.
