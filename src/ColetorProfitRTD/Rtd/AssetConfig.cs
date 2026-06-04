using System;
using System.Collections.Generic;
using System.Linq;

namespace ColetorProfitRTD.Rtd
{
    public sealed class AssetConfig
    {
        public string Id { get; set; }
        public string Asset { get; set; }
        public string Label { get; set; }
        public bool Enabled { get; set; } = true;
        public RtdSourceConfig PriceRtd { get; set; }
        public RtdSourceConfig BookRtd { get; set; }
        public RtdSourceConfig TimesRtd { get; set; }
        public AssetHistoryInfo History { get; set; } = new AssetHistoryInfo();

        public static AssetConfig CreateDefault(string asset, string progId)
        {
            string normalized = NormalizeAsset(asset) ?? "WDOFUT_F_0";

            return new AssetConfig
            {
                Id = normalized,
                Asset = normalized,
                Label = normalized,
                Enabled = true,
                PriceRtd = RtdSourceConfig.Price(normalized, progId),
                BookRtd = RtdSourceConfig.Book("BOOK0", progId),
                TimesRtd = RtdSourceConfig.Times("T&T0", progId),
                History = new AssetHistoryInfo()
            };
        }

        public AssetConfig Clone()
        {
            return new AssetConfig
            {
                Id = Id,
                Asset = Asset,
                Label = Label,
                Enabled = Enabled,
                PriceRtd = PriceRtd == null ? null : PriceRtd.Clone(),
                BookRtd = BookRtd == null ? null : BookRtd.Clone(),
                TimesRtd = TimesRtd == null ? null : TimesRtd.Clone(),
                History = History == null ? new AssetHistoryInfo() : History.Clone()
            };
        }

        public void Normalize(string progId)
        {
            Asset = NormalizeAsset(Asset ?? Id) ?? "WDOFUT_F_0";
            Id = NormalizeAsset(Id ?? Asset) ?? Asset;
            Label = string.IsNullOrWhiteSpace(Label) ? Asset : Label.Trim();
            PriceRtd = PriceRtd ?? RtdSourceConfig.Price(Asset, progId);
            BookRtd = BookRtd ?? RtdSourceConfig.Book("BOOK0", progId);
            TimesRtd = TimesRtd ?? RtdSourceConfig.Times("T&T0", progId);
            History = History ?? new AssetHistoryInfo();
            PriceRtd.Normalize(RtdChannel.Price, Asset, progId);
            BookRtd.Normalize(RtdChannel.Book, "BOOK0", progId);
            TimesRtd.Normalize(RtdChannel.TimesTrades, "T&T0", progId);
        }

        public IEnumerable<RtdSourceConfig> Sources()
        {
            if (PriceRtd != null)
            {
                yield return PriceRtd;
            }

            if (BookRtd != null)
            {
                yield return BookRtd;
            }

            if (TimesRtd != null)
            {
                yield return TimesRtd;
            }
        }

        public static string NormalizeAsset(string asset)
        {
            return string.IsNullOrWhiteSpace(asset) ? null : asset.Trim().ToUpperInvariant();
        }
    }

    public sealed class RtdSourceConfig
    {
        public string Channel { get; set; }
        public bool Enabled { get; set; } = true;
        public string ProgId { get; set; } = "RTDTrading.RTDServer";
        public string Topic { get; set; }
        public int Depth { get; set; }
        public int Rows { get; set; }
        public List<string> Fields { get; set; } = new List<string>();

        public static RtdSourceConfig Price(string topic, string progId)
        {
            return new RtdSourceConfig
            {
                Channel = RtdChannel.Price,
                Enabled = true,
                ProgId = progId,
                Topic = topic,
                Fields = RtdFieldCatalog.DefaultPriceFields.ToList()
            };
        }

        public static RtdSourceConfig Book(string topic, string progId)
        {
            return new RtdSourceConfig
            {
                Channel = RtdChannel.Book,
                Enabled = true,
                ProgId = progId,
                Topic = topic,
                Depth = 50,
                Fields = RtdFieldCatalog.DefaultBookFields.ToList()
            };
        }

        public static RtdSourceConfig Times(string topic, string progId)
        {
            return new RtdSourceConfig
            {
                Channel = RtdChannel.TimesTrades,
                Enabled = true,
                ProgId = progId,
                Topic = topic,
                Rows = 100,
                Fields = RtdFieldCatalog.DefaultTimesFields.ToList()
            };
        }

        public RtdSourceConfig Clone()
        {
            return new RtdSourceConfig
            {
                Channel = Channel,
                Enabled = Enabled,
                ProgId = ProgId,
                Topic = Topic,
                Depth = Depth,
                Rows = Rows,
                Fields = Fields == null ? new List<string>() : Fields.ToList()
            };
        }

        public void Normalize(string channel, string fallbackTopic, string progId)
        {
            Channel = RtdChannel.Normalize(Channel) ?? channel;
            ProgId = string.IsNullOrWhiteSpace(ProgId) ? progId : ProgId.Trim();
            Topic = string.IsNullOrWhiteSpace(Topic) ? fallbackTopic : Topic.Trim().ToUpperInvariant();

            if (Channel == RtdChannel.Price)
            {
                Fields = NormalizeFields(Fields, RtdFieldCatalog.DefaultPriceFields);
            }
            else if (Channel == RtdChannel.Book)
            {
                Depth = Depth <= 0 ? 50 : Math.Min(Depth, 200);
                Fields = NormalizeFields(Fields, RtdFieldCatalog.DefaultBookFields);
            }
            else if (Channel == RtdChannel.TimesTrades)
            {
                Rows = Rows <= 0 ? 100 : Math.Min(Rows, 300);
                Fields = NormalizeFields(Fields, RtdFieldCatalog.DefaultTimesFields);
            }
        }

        private static List<string> NormalizeFields(List<string> fields, IReadOnlyList<string> fallback)
        {
            IEnumerable<string> source = fields == null || fields.Count == 0 ? fallback : fields;

            return source
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public sealed class AssetHistoryInfo
    {
        public bool HasCsv { get; set; }
        public string FileName { get; set; }
        public string SavedAt { get; set; }
        public int Rows { get; set; }
        public long Bytes { get; set; }

        public AssetHistoryInfo Clone()
        {
            return new AssetHistoryInfo
            {
                HasCsv = HasCsv,
                FileName = FileName,
                SavedAt = SavedAt,
                Rows = Rows,
                Bytes = Bytes
            };
        }
    }

    public static class RtdChannel
    {
        public const string Price = "price";
        public const string Book = "book";
        public const string TimesTrades = "timesTrades";

        public static string Normalize(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                return null;
            }

            string value = channel.Trim();

            if (value.Equals("price", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("preco", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("preço", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("quote", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("cotacao", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("cotação", StringComparison.OrdinalIgnoreCase))
            {
                return Price;
            }

            if (value.Equals("book", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("ofertas", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("bookOfertas", StringComparison.OrdinalIgnoreCase))
            {
                return Book;
            }

            if (value.Equals("times", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("trades", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("timesTrades", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("timesAndTrades", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("times & trades", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("t&t", StringComparison.OrdinalIgnoreCase))
            {
                return TimesTrades;
            }

            return value;
        }
    }
}
