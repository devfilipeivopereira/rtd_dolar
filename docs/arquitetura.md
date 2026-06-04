# Arquitetura

## Objetivo

O projeto transforma o RTD do Profit em uma fonte local de dados em tempo real para uma interface HTML. O Excel nao entra no fluxo: o aplicativo C# assume o papel de cliente RTD.

```text
Profit Pro
  -> RTDTrading.RTDServer
  -> RtdClient C# em thread STA
  -> MarketSnapshot / BookDepth / TimesTrades por ativo
  -> WebSocket ws://localhost:5000/ws
  -> Dashboard HTML
```

## Backend

O backend fica em `src/ColetorProfitRTD`.

- `Rtd/`: interfaces COM, catalogo de campos e cliente RTD.
- `MarketData/`: parsing pt-BR e snapshots consolidados por ativo.
- `Web/`: `HttpListener`, arquivos estaticos, `/health`, `/snapshot`, `/flow`, `/signals`, `/assets` e `/ws`.
- `Storage/`: SQLite auxiliar para snapshots e buckets por minuto.
- `Flow/`: order flow derivado por ativo, com delta, VWAP, profile intraday e sinais.

O RTD roda em uma thread STA dedicada. Isso evita misturar chamadas COM com o loop HTTP/WebSocket.

O cadastro de ativos tem dois niveis:

- `appsettings.json`: semente inicial quando ainda nao existe cadastro persistido;
- `data/assets/assets.json`: cadastro runtime salvo pela tela `Ativos`.

Durante a execucao, o dashboard chama `POST /assets` para criar/atualizar ativo e `POST /assets/toggle` para ligar/desligar o ativo inteiro ou uma fonte especifica. Desligar remove os topicos RTD daquele ativo no servidor COM.

Tambem existem:

- `POST /assets/channels`: troca os canais RTD assinados para um ativo;
- `POST /assets/delete` ou `DELETE /assets`: remove o ativo da lista, desassina os topicos e limpa o snapshot em memoria.
- `POST /assets/history`: salva o CSV historico do ativo em `data/assets/{ativo}/history.csv`;
- `GET /assets/history?asset=...`: devolve o CSV salvo para recarregar o Panorama.

Os canais atuais sao:

- `price`: topico do ativo, como `WDON26_G_0`;
- `book`: topico `BOOKn`, com `INFO/ATV`, `INFO/TAB` e campos por nivel;
- `timesTrades`: topico `T&Tn`, com `INFO/ATV`, `INFO/TAB` e campos por linha.

O cliente RTD aceita argumentos variaveis. Preco usa `ativo, campo`; book usa `topico, campo, nivel`; Times & Trades usa `topico, campo, linha`.

## Frontend

O frontend fica em `src/dashboard/index.html` e usa o HTML do `dolar-points` como base.

O CSV diario continua sendo a fonte do historico 21/45/63. O RTD preenche o intraday:

- abertura;
- maxima parcial;
- minima parcial;
- preco atual;
- VWAP/ancora opcional;
- volume acumulado;
- book e volume projetado.

O menu superior separa:

- `Ativos`: cadastro de codigo, tres fontes RTD e CSV historico;
- `Cotacoes`: watchlist operacional com todos os ativos cadastrados;
- `Grafico`: grafico e subabas analiticas;
- `DOM`: escada de preco;
- `Book`: book de ofertas multi-nivel;
- `T&T`: Times & Trades RTD;
- `Fluxo`: delta, book, tape e metricas;
- `Setups`: sinais;
- `Boleta`: plano local de entrada, stop, alvo e acompanhamento por toque de preco;
- `Alertas`: alertas locais por preco;
- `Risco`: calculadora local de stop, alvo e contratos;
- `Historico`: resumo do CSV e ticks em memoria;
- `Sistema`: saude e debug.

A faixa superior fica disponivel em todas as telas e mostra ativo selecionado, ultimo preco, bid/ask, status de Book, status de Times, delta 5s e CSV carregado.

A tela `Ativos` configura fontes e CSV. A tela `Cotacoes` e a mesa de monitoramento: cada linha mostra ultimo preco, bid/ask, delta, status de Book, status de Times, status das fontes `P/B/T` e botoes para abrir `Grafico`, `DOM`, `Book`, `T&T` ou `Boleta` daquele ativo.

A `Boleta` persiste planos no navegador por ativo. Ela calcula risco e R/R, acompanha se entrada, alvo ou stop foram tocados pelo preco RTD e nao chama nenhum endpoint de envio de ordem.

A selecao de ativo define o que aparece nos campos intraday, DOM, fluxo e setups. Snapshots, book e Times & Trades de ativos diferentes ficam em caches separados no navegador, e as fontes podem ser ajustadas por ativo sem reiniciar o app.

## Aba DOM

A aba `DOM` mostra:

- escada em tick size de 0,5;
- ultimo preco ao centro;
- bid/ask do snapshot de preco;
- volumes de book multi-nivel quando `bookDepth` chega;
- tape real quando `timesTrades` chega, com fallback para tape derivado;
- pontos principais do motor quant quando o CSV ja foi carregado.

Os pontos marcados incluem abertura, maxima, minima, VWAP/MED, POC, VAH, VAL, desvios por abertura, desvios por POC e confluencias.

As mensagens auxiliares de profundidade sao coalescidas no cliente RTD para reduzir repintura da UI: `bookDepth` tem broadcast minimo de 100 ms e `timesTrades` de 150 ms. Snapshots de preco continuam no fluxo existente.

## Persistencia

SQLite e auxiliar. Se o provider SQLite falhar, o RTD e o WebSocket continuam rodando.

Tabelas:

- `snapshots`: ultimo estado consolidado salvo por intervalo.
- `minute_snapshots`: ultimo estado de cada minuto por ativo.
