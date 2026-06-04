using System.Collections.Generic;
using System.Linq;

namespace ColetorProfitRTD.Rtd
{
    public sealed class RtdFieldInfo
    {
        public RtdFieldInfo(string code, string label, bool defaultLive = false)
        {
            Code = code;
            Label = label;
            DefaultLive = defaultLive;
        }

        public string Code { get; }
        public string Label { get; }
        public bool DefaultLive { get; }
    }

    public static class RtdFieldCatalog
    {
        public const string QuoteChannel = RtdChannel.Price;
        public const string PriceChannel = RtdChannel.Price;
        public const string BookChannel = "book";
        public const string TimesTradesChannel = "timesTrades";

        public static readonly IReadOnlyList<RtdFieldInfo> Fields = new[]
        {
            new RtdFieldInfo("DAT", "Data", true),
            new RtdFieldInfo("HOR", "Hora", true),
            new RtdFieldInfo("ULT", "Ultimo", true),
            new RtdFieldInfo("ABE", "Abertura", true),
            new RtdFieldInfo("MAX", "Maximo", true),
            new RtdFieldInfo("MIN", "Minimo", true),
            new RtdFieldInfo("FEC", "Fechamento Anterior", true),
            new RtdFieldInfo("PEX", "Strike"),
            new RtdFieldInfo("VAR", "Variacao", true),
            new RtdFieldInfo("VARPTS", "Variacao em pontos", true),
            new RtdFieldInfo("MED", "Media", true),
            new RtdFieldInfo("NEG", "Negocios", true),
            new RtdFieldInfo("QUL", "Quantidade do ultimo negocio", true),
            new RtdFieldInfo("QTT", "Quantidade", true),
            new RtdFieldInfo("VOL", "Volume", true),
            new RtdFieldInfo("OCP", "Oferta de compra", true),
            new RtdFieldInfo("OVD", "Oferta de venda", true),
            new RtdFieldInfo("VOC", "Volume oferta compra", true),
            new RtdFieldInfo("VOV", "Volume oferta venda", true),
            new RtdFieldInfo("AJU", "Ajuste", true),
            new RtdFieldInfo("AJA", "Ajuste anterior", true),
            new RtdFieldInfo("PRT", "Preco teorico"),
            new RtdFieldInfo("QTE", "Quantidade teorica"),
            new RtdFieldInfo("VPJ", "Volume projetado", true),
            new RtdFieldInfo("SEM", "Semana"),
            new RtdFieldInfo("MES", "Mes"),
            new RtdFieldInfo("3M", "3 meses"),
            new RtdFieldInfo("6M", "6 meses"),
            new RtdFieldInfo("12M", "12 meses"),
            new RtdFieldInfo("ANO", "Ano"),
            new RtdFieldInfo("TRIM", "Trimestre"),
            new RtdFieldInfo("SEMES", "Semestre"),
            new RtdFieldInfo("VEN", "Vencimento", true),
            new RtdFieldInfo("VAL", "Validade", true),
            new RtdFieldInfo("CAB", "Contratos abertos", true),
            new RtdFieldInfo("EST", "Estado atual", true),
            new RtdFieldInfo("BLACK", "Black Scholes"),
            new RtdFieldInfo("IMPVT", "Volatilidade implicita"),
            new RtdFieldInfo("DELTA", "Delta"),
            new RtdFieldInfo("GAMA", "Gama"),
            new RtdFieldInfo("THETA", "Theta"),
            new RtdFieldInfo("RHO", "Rho"),
            new RtdFieldInfo("VEGA", "Vega"),
            new RtdFieldInfo("VIA", "VI Ask"),
            new RtdFieldInfo("VIB", "VI Bid"),
            new RtdFieldInfo("DOBRAR", "Dobrar %"),
            new RtdFieldInfo("VIVH", "VI / VH"),
            new RtdFieldInfo("VINT", "Valor intrinseco"),
            new RtdFieldInfo("VEXT", "Valor extrinseco"),
            new RtdFieldInfo("8", "Acumulacao/Distribuicao"),
            new RtdFieldInfo("88", "Acumulacao/Distribuicao Williams"),
            new RtdFieldInfo("124", "Adaptive Moving Average (AMA)"),
            new RtdFieldInfo("22", "ADX"),
            new RtdFieldInfo("126", "Afastamento Medio"),
            new RtdFieldInfo("16", "Arms Ease of Movement"),
            new RtdFieldInfo("51", "Aroon Linha"),
            new RtdFieldInfo("49", "Aroon Oscilador"),
            new RtdFieldInfo("27", "Balanca de Poder"),
            new RtdFieldInfo("12", "Bandas de Bollinger"),
            new RtdFieldInfo("32", "Bear Power"),
            new RtdFieldInfo("41", "Bollinger b%"),
            new RtdFieldInfo("42", "Bollinger Band Width"),
            new RtdFieldInfo("31", "Bull Power"),
            new RtdFieldInfo("73", "Canal Donchian"),
            new RtdFieldInfo("82", "Candle Code"),
            new RtdFieldInfo("5", "CCI Linha")
        };

        public static readonly IReadOnlyList<string> DefaultLiveFields = Fields
            .Where(x => x.DefaultLive)
            .Select(x => x.Code)
            .ToList();

        public static readonly IReadOnlyList<string> DefaultPriceFields = new[]
        {
            "DAT", "HOR", "ULT", "ABE", "MAX", "MIN", "FEC", "NEG", "QTT", "VOL",
            "OCP", "OVD", "AJU", "AJA", "103", "98", "100", "99", "67"
        };

        public static readonly IReadOnlyList<string> DefaultBookFields = new[]
        {
            "HORC", "ACP", "VOC", "OCP", "OVD", "VOV", "AVD", "HORV"
        };

        public static readonly IReadOnlyList<string> DefaultTimesFields = new[]
        {
            "DAT", "ACP", "PRE", "QUL", "AVD", "AGR"
        };

        public static readonly IReadOnlyList<string> DefaultChannels = new[]
        {
            PriceChannel,
            BookChannel,
            TimesTradesChannel
        };

        public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultChannelFields =
            new Dictionary<string, IReadOnlyList<string>>(System.StringComparer.OrdinalIgnoreCase)
            {
                [QuoteChannel] = new[]
                {
                    "DAT", "HOR", "ULT", "ABE", "MAX", "MIN", "FEC", "NEG", "QTT", "VOL",
                    "OCP", "OVD", "AJU", "AJA", "103", "98", "100", "99", "67"
                },
                [BookChannel] = new[]
                {
                    "OCP", "OVD", "VOC", "VOV", "VPJ"
                },
                [TimesTradesChannel] = new[]
                {
                    "DAT", "HOR", "ULT", "QUL", "NEG", "QTT", "VOL"
                }
            };

        public static string NormalizeChannel(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                return null;
            }

            string value = channel.Trim();

            if (value.Equals("preco", System.StringComparison.OrdinalIgnoreCase) ||
                value.Equals("preço", System.StringComparison.OrdinalIgnoreCase) ||
                value.Equals("price", System.StringComparison.OrdinalIgnoreCase) ||
                value.Equals("cotacao", System.StringComparison.OrdinalIgnoreCase) ||
                value.Equals("cotação", System.StringComparison.OrdinalIgnoreCase) ||
                value.Equals("quote", System.StringComparison.OrdinalIgnoreCase))
            {
                return PriceChannel;
            }

            if (value.Equals("book", System.StringComparison.OrdinalIgnoreCase) ||
                value.Equals("ofertas", System.StringComparison.OrdinalIgnoreCase) ||
                value.Equals("bookOfertas", System.StringComparison.OrdinalIgnoreCase))
            {
                return BookChannel;
            }

            if (value.Equals("times", System.StringComparison.OrdinalIgnoreCase) ||
                value.Equals("trades", System.StringComparison.OrdinalIgnoreCase) ||
                value.Equals("timesTrades", System.StringComparison.OrdinalIgnoreCase) ||
                value.Equals("timesAndTrades", System.StringComparison.OrdinalIgnoreCase))
            {
                return TimesTradesChannel;
            }

            return value;
        }
    }
}
