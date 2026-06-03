using System;
using System.Collections.Generic;

namespace ColetorProfitRTD.MarketData
{
    public sealed class MarketSnapshot
    {
        public DateTimeOffset LocalTimestamp { get; set; } = DateTimeOffset.Now;
        public string Asset { get; set; }
        public string Status { get; set; } = "starting";
        public Dictionary<string, object> Rtd { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Raw { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string DataProfit => GetText("DAT");
        public string HoraProfit => GetText("HOR");
        public decimal? Abertura => GetDecimal("ABE");
        public decimal? Maxima => GetDecimal("MAX");
        public decimal? Minima => GetDecimal("MIN");
        public decimal? FechamentoAnterior => GetDecimal("FEC");
        public decimal? Ultimo => GetDecimal("ULT");
        public decimal? Media => GetDecimal("MED");
        public decimal? Volume => GetDecimal("VOL");
        public decimal? Quantidade => GetDecimal("QTT");
        public decimal? Negocios => GetDecimal("NEG");
        public decimal? OfertaCompra => GetDecimal("OCP");
        public decimal? OfertaVenda => GetDecimal("OVD");
        public decimal? VolumeOfertaCompra => GetDecimal("VOC");
        public decimal? VolumeOfertaVenda => GetDecimal("VOV");
        public decimal? VolumeProjetado => GetDecimal("VPJ");

        public MarketSnapshot Clone()
        {
            return new MarketSnapshot
            {
                LocalTimestamp = LocalTimestamp,
                Asset = Asset,
                Status = Status,
                Rtd = new Dictionary<string, object>(Rtd, StringComparer.OrdinalIgnoreCase),
                Raw = new Dictionary<string, string>(Raw, StringComparer.OrdinalIgnoreCase)
            };
        }

        public Dictionary<string, object> ToLiveMessage()
        {
            return new Dictionary<string, object>
            {
                ["type"] = "snapshot",
                ["asset"] = Asset,
                ["status"] = Status,
                ["localTimestamp"] = LocalTimestamp.ToString("o"),
                ["rtd"] = Rtd,
                ["raw"] = Raw,
                ["intraday"] = new Dictionary<string, object>
                {
                    ["abertura"] = Abertura,
                    ["maximaParcial"] = Maxima,
                    ["minimaParcial"] = Minima,
                    ["precoAtual"] = Ultimo,
                    ["vwapIntraday"] = Media,
                    ["volumeAcumulado"] = Volume,
                    ["fechamentoAnterior"] = FechamentoAnterior,
                    ["quantidade"] = Quantidade,
                    ["negocios"] = Negocios
                },
                ["book"] = new Dictionary<string, object>
                {
                    ["ofertaCompra"] = OfertaCompra,
                    ["ofertaVenda"] = OfertaVenda,
                    ["volumeOfertaCompra"] = VolumeOfertaCompra,
                    ["volumeOfertaVenda"] = VolumeOfertaVenda,
                    ["volumeProjetado"] = VolumeProjetado
                }
            };
        }

        public decimal? GetFieldDecimal(string field)
        {
            return GetDecimal(field);
        }

        private decimal? GetDecimal(string field)
        {
            if (!Rtd.TryGetValue(field, out object value))
            {
                return null;
            }

            return ValueParser.ToDecimal(value);
        }

        private string GetText(string field)
        {
            if (!Rtd.TryGetValue(field, out object value))
            {
                return null;
            }

            return ValueParser.ToText(value);
        }
    }
}
