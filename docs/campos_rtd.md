# Campos RTD

## Fontes RTD assinadas por padrao

O cadastro atual e feito na tela `Ativos` e salvo em `data/assets/assets.json`. Cada ativo pode ter tres fontes:

- `Preco`: topico do ativo, por exemplo `WDON26_G_0`;
- `Book`: topico de book, por exemplo `BOOK0`;
- `Times & Trades`: topico de negocios, por exemplo `T&T0`.

`appsettings.json` continua existindo como semente inicial e compatibilidade.

## Preco e indicadores

| Codigo | Uso |
|---|---|
| DAT | Data do Profit |
| HOR | Hora do Profit |
| ULT | Preco atual |
| ABE | Abertura |
| MAX | Maxima parcial |
| MIN | Minima parcial |
| FEC | Fechamento anterior |
| NEG | Negocios |
| QTT | Quantidade |
| VOL | Volume acumulado |
| OCP | Oferta de compra |
| OVD | Oferta de venda |
| AJU | Ajuste |
| AJA | Ajuste anterior |
| 103 | TR - Saldo acumulado de agressao |
| 98 | TR - Volume de agressao compra |
| 100 | TR - Volume de agressao saldo |
| 99 | TR - Volume de agressao venda |
| 67 | VWAP |

## Book de ofertas

Formula especial:

```text
=RTD("rtdtrading.rtdserver";; "BOOK0"; "INFO"; "ATV")
=RTD("rtdtrading.rtdserver";; "BOOK0"; "INFO"; "TAB")
```

Por nivel, de `0` a `49` por padrao:

```text
=RTD("rtdtrading.rtdserver";; "BOOK0"; "OCP"; 0)
```

Campos:

| Codigo | Uso |
|---|---|
| HORC | Hora da compra |
| ACP | Agente comprador |
| VOC | Quantidade na compra |
| OCP | Preco de compra |
| OVD | Preco de venda |
| VOV | Quantidade na venda |
| AVD | Agente vendedor |
| HORV | Hora da venda |

## Times & Trades

Formula especial:

```text
=RTD("rtdtrading.rtdserver";; "T&T0"; "INFO"; "ATV")
=RTD("rtdtrading.rtdserver";; "T&T0"; "INFO"; "TAB")
```

Por linha, de `0` a `99` por padrao:

```text
=RTD("rtdtrading.rtdserver";; "T&T0"; "PRE"; 0)
```

Campos:

| Codigo | Uso |
|---|---|
| DAT | Data/hora da linha |
| ACP | Compradora |
| PRE | Preco |
| QUL | Quantidade |
| AVD | Vendedora |
| AGR | Agressor |

## Formula RTD simples de preco

Formato usado pelo Profit:

```text
=RTD("RTDTrading.RTDServer";; "WDOFUT_F_0"; "ULT")
```

No app, a tela `Ativos` monta esta chamada sem colagem livre de formula:

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

## Canais por ativo

Os canais atuais agrupam os campos assim:

| Canal | Campos | Uso |
|---|---|---|
| `price` | `DAT`, `HOR`, `ULT`, `ABE`, `MAX`, `MIN`, `FEC`, `NEG`, `QTT`, `VOL`, `OCP`, `OVD`, `AJU`, `AJA`, `103`, `98`, `100`, `99`, `67` | preco, intraday e indicadores |
| `book` | `INFO/ATV`, `INFO/TAB`, `HORC`, `ACP`, `VOC`, `OCP`, `OVD`, `VOV`, `AVD`, `HORV` por nivel | book multi-nivel |
| `timesTrades` | `INFO/ATV`, `INFO/TAB`, `DAT`, `ACP`, `PRE`, `QUL`, `AVD`, `AGR` por linha | Times & Trades real |

Quando uma fonte e alterada na tela `Ativos`, o app desassina e reassina o ativo com os topicos novos.
