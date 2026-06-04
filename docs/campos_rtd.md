# Campos RTD

## Campos assinados por padrao

Estes campos estao em `src/ColetorProfitRTD/appsettings.json` e sao suficientes para o MVP:

O mesmo conjunto de campos e assinado para cada ativo ligado em `Rtd.ActiveAssets` ou pelo controle `Ativos RTD` do dashboard.

| Codigo | Uso |
|---|---|
| DAT | Data do Profit |
| HOR | Hora do Profit |
| ULT | Preco atual |
| ABE | Abertura |
| MAX | Maxima parcial |
| MIN | Minima parcial |
| FEC | Fechamento anterior |
| VAR | Variacao percentual |
| VARPTS | Variacao em pontos |
| MED | Media, usada como proxy opcional de VWAP/ancora |
| NEG | Negocios |
| QUL | Quantidade do ultimo negocio |
| QTT | Quantidade |
| VOL | Volume acumulado |
| OCP | Oferta de compra |
| OVD | Oferta de venda |
| VOC | Volume oferta compra |
| VOV | Volume oferta venda |
| AJU | Ajuste |
| AJA | Ajuste anterior |
| VPJ | Volume projetado |
| VEN | Vencimento |
| VAL | Validade |
| CAB | Contratos abertos |
| EST | Estado atual |

## Formula RTD

Formato usado pelo Profit:

```text
=RTD("RTDTrading.RTDServer";; "WDOFUT_F_0"; "ULT")
```

No app:

- servidor COM: `RTDTrading.RTDServer`;
- ativo: `WDOFUT_F_0`, `WINFUT_F_0` ou outro simbolo aceito pelo Profit;
- campo: codigo RTD, por exemplo `ULT`, `VOL`, `OCP`.

## Catalogo completo

O catalogo fica em `src/ColetorProfitRTD/Rtd/RtdFieldCatalog.cs`.

Ele inclui campos de preco, book, vencimento, opcoes e indicadores numericos colados a partir da lista do Profit, como:

- `BLACK`, `IMPVT`, `DELTA`, `GAMA`, `THETA`, `RHO`, `VEGA`;
- `VIA`, `VIB`, `DOBRAR`, `VIVH`, `VINT`, `VEXT`;
- indicadores numericos como `8`, `88`, `124`, `22`, `126`, `16`, `51`, `49`, `27`, `12`, `32`, `41`, `42`, `31`, `73`, `82`, `5`.

Para assinar um novo campo, adicione o codigo em `Rtd.Fields` no `appsettings.json`.

Para cadastrar ativos iniciais, use `Rtd.Assets`. Para decidir quais ja iniciam ligados, use `Rtd.ActiveAssets`.
