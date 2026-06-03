using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Threading;
using ColetorProfitRTD.Flow;
using ColetorProfitRTD.MarketData;
using ColetorProfitRTD.Web;

namespace ColetorProfitRTD.Storage
{
    public sealed class SqliteSnapshotStore
    {
        private readonly StorageConfig _config;
        private readonly Logger _log;
        private readonly object _lock = new object();
        private DateTime _lastSaveUtc = DateTime.MinValue;
        private DateTime _lastFlowSaveUtc = DateTime.MinValue;
        private int _saving;
        private int _flowSaving;
        private bool _initialized;

        public SqliteSnapshotStore(StorageConfig config, Logger log)
        {
            _config = config;
            _log = log;
        }

        public bool Enabled => _config.Enabled;

        public void Initialize()
        {
            if (!_config.Enabled)
            {
                _log.Info("SQLite desabilitado por configuracao.");
                return;
            }

            EnsureDatabaseDirectory();

            using (var connection = new SQLiteConnection(_config.ConnectionString))
            {
                connection.Open();

                Execute(connection, @"
CREATE TABLE IF NOT EXISTS snapshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    local_timestamp TEXT NOT NULL,
    asset TEXT,
    status TEXT,
    abertura REAL,
    maxima REAL,
    minima REAL,
    ultimo REAL,
    media REAL,
    volume REAL,
    quantidade REAL,
    negocios REAL,
    oferta_compra REAL,
    oferta_venda REAL,
    volume_oferta_compra REAL,
    volume_oferta_venda REAL,
    volume_projetado REAL,
    raw_json TEXT NOT NULL
);");

                Execute(connection, @"
CREATE TABLE IF NOT EXISTS minute_snapshots (
    trade_date TEXT NOT NULL,
    time_bucket TEXT NOT NULL,
    asset TEXT NOT NULL,
    ultimo REAL,
    volume REAL,
    negocios REAL,
    raw_json TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    PRIMARY KEY (trade_date, time_bucket, asset)
);");

                Execute(connection, @"
CREATE TABLE IF NOT EXISTS flow_metrics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    local_timestamp TEXT NOT NULL,
    asset TEXT,
    data_quality TEXT,
    last_price REAL,
    delta_5s REAL,
    cumulative_delta REAL,
    top_book_imbalance REAL,
    order_flow_imbalance REAL,
    vwap REAL,
    poc REAL,
    raw_json TEXT NOT NULL
);");

                Execute(connection, @"
CREATE TABLE IF NOT EXISTS flow_signals (
    id TEXT PRIMARY KEY,
    local_timestamp TEXT NOT NULL,
    asset TEXT,
    setup TEXT,
    direction TEXT,
    price REAL,
    score REAL,
    confidence TEXT,
    data_quality TEXT,
    raw_json TEXT NOT NULL
);");
            }

            _initialized = true;
            _log.Info("SQLite inicializado.");
        }

        public void QueueSaveFlowMetrics(FlowMetrics metrics)
        {
            if (!_config.Enabled || !_initialized || metrics == null)
            {
                return;
            }

            lock (_lock)
            {
                TimeSpan elapsed = DateTime.UtcNow - _lastFlowSaveUtc;

                if (elapsed.TotalMilliseconds < Math.Max(_config.SnapshotIntervalMs, 250))
                {
                    return;
                }

                _lastFlowSaveUtc = DateTime.UtcNow;
            }

            if (Interlocked.CompareExchange(ref _flowSaving, 1, 0) != 0)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    SaveFlowMetrics(metrics);
                }
                catch (Exception ex)
                {
                    _log.Error("Falha ao salvar metricas de fluxo SQLite.", ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _flowSaving, 0);
                }
            });
        }

        public void QueueSaveSignal(FlowSignal signal)
        {
            if (!_config.Enabled || !_initialized || signal == null)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    SaveSignal(signal);
                }
                catch (Exception ex)
                {
                    _log.Error("Falha ao salvar sinal de fluxo SQLite.", ex);
                }
            });
        }

        public void QueueSave(MarketSnapshot snapshot)
        {
            if (!_config.Enabled || !_initialized || snapshot == null)
            {
                return;
            }

            lock (_lock)
            {
                TimeSpan elapsed = DateTime.UtcNow - _lastSaveUtc;

                if (elapsed.TotalMilliseconds < Math.Max(_config.SnapshotIntervalMs, 250))
                {
                    return;
                }

                _lastSaveUtc = DateTime.UtcNow;
            }

            if (Interlocked.CompareExchange(ref _saving, 1, 0) != 0)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Save(snapshot);
                }
                catch (Exception ex)
                {
                    _log.Error("Falha ao salvar snapshot SQLite.", ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _saving, 0);
                }
            });
        }

        private void Save(MarketSnapshot snapshot)
        {
            using (var connection = new SQLiteConnection(_config.ConnectionString))
            {
                connection.Open();

                using (SQLiteTransaction tx = connection.BeginTransaction())
                {
                    InsertSnapshot(connection, tx, snapshot);
                    UpsertMinuteSnapshot(connection, tx, snapshot);
                    tx.Commit();
                }
            }
        }

        private void SaveFlowMetrics(FlowMetrics metrics)
        {
            using (var connection = new SQLiteConnection(_config.ConnectionString))
            {
                connection.Open();

                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO flow_metrics (
    local_timestamp, asset, data_quality, last_price, delta_5s, cumulative_delta,
    top_book_imbalance, order_flow_imbalance, vwap, poc, raw_json
) VALUES (
    @local_timestamp, @asset, @data_quality, @last_price, @delta_5s, @cumulative_delta,
    @top_book_imbalance, @order_flow_imbalance, @vwap, @poc, @raw_json
);";

                    command.Parameters.AddWithValue("@local_timestamp", metrics.Timestamp.ToString("o"));
                    command.Parameters.AddWithValue("@asset", metrics.Asset ?? string.Empty);
                    command.Parameters.AddWithValue("@data_quality", FlowProcessor.QualityName(metrics.DataQuality));
                    command.Parameters.AddWithValue("@last_price", DbValue(metrics.LastPrice));
                    command.Parameters.AddWithValue("@delta_5s", metrics.Delta5s);
                    command.Parameters.AddWithValue("@cumulative_delta", metrics.CumulativeDelta);
                    command.Parameters.AddWithValue("@top_book_imbalance", DbValue(metrics.TopBookImbalance));
                    command.Parameters.AddWithValue("@order_flow_imbalance", DbValue(metrics.OrderFlowImbalance));
                    command.Parameters.AddWithValue("@vwap", DbValue(metrics.Vwap));
                    command.Parameters.AddWithValue("@poc", DbValue(metrics.Poc));
                    command.Parameters.AddWithValue("@raw_json", JsonHelper.Serialize(metrics.ToMessage()));
                    command.ExecuteNonQuery();
                }
            }
        }

        private void SaveSignal(FlowSignal signal)
        {
            using (var connection = new SQLiteConnection(_config.ConnectionString))
            {
                connection.Open();

                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT OR REPLACE INTO flow_signals (
    id, local_timestamp, asset, setup, direction, price, score, confidence, data_quality, raw_json
) VALUES (
    @id, @local_timestamp, @asset, @setup, @direction, @price, @score, @confidence, @data_quality, @raw_json
);";

                    command.Parameters.AddWithValue("@id", signal.Id ?? Guid.NewGuid().ToString("N"));
                    command.Parameters.AddWithValue("@local_timestamp", signal.Timestamp.ToString("o"));
                    command.Parameters.AddWithValue("@asset", signal.Asset ?? string.Empty);
                    command.Parameters.AddWithValue("@setup", signal.Setup ?? string.Empty);
                    command.Parameters.AddWithValue("@direction", signal.Direction ?? string.Empty);
                    command.Parameters.AddWithValue("@price", DbValue(signal.Price));
                    command.Parameters.AddWithValue("@score", signal.Score);
                    command.Parameters.AddWithValue("@confidence", signal.Confidence ?? string.Empty);
                    command.Parameters.AddWithValue("@data_quality", FlowProcessor.QualityName(signal.DataQuality));
                    command.Parameters.AddWithValue("@raw_json", JsonHelper.Serialize(signal.ToMessage()));
                    command.ExecuteNonQuery();
                }
            }
        }

        private void InsertSnapshot(SQLiteConnection connection, SQLiteTransaction tx, MarketSnapshot snapshot)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = tx;
                command.CommandText = @"
INSERT INTO snapshots (
    local_timestamp, asset, status, abertura, maxima, minima, ultimo, media, volume,
    quantidade, negocios, oferta_compra, oferta_venda, volume_oferta_compra,
    volume_oferta_venda, volume_projetado, raw_json
) VALUES (
    @local_timestamp, @asset, @status, @abertura, @maxima, @minima, @ultimo, @media, @volume,
    @quantidade, @negocios, @oferta_compra, @oferta_venda, @volume_oferta_compra,
    @volume_oferta_venda, @volume_projetado, @raw_json
);";

                AddCommonParameters(command, snapshot);
                command.ExecuteNonQuery();
            }
        }

        private void UpsertMinuteSnapshot(SQLiteConnection connection, SQLiteTransaction tx, MarketSnapshot snapshot)
        {
            DateTimeOffset timestamp = snapshot.LocalTimestamp;
            string tradeDate = timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string bucket = timestamp.ToString("HH:mm", CultureInfo.InvariantCulture);

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = tx;
                command.CommandText = @"
INSERT OR REPLACE INTO minute_snapshots (
    trade_date, time_bucket, asset, ultimo, volume, negocios, raw_json, updated_at
) VALUES (
    @trade_date, @time_bucket, @asset, @ultimo, @volume, @negocios, @raw_json, @updated_at
);";

                command.Parameters.AddWithValue("@trade_date", tradeDate);
                command.Parameters.AddWithValue("@time_bucket", bucket);
                command.Parameters.AddWithValue("@asset", snapshot.Asset ?? string.Empty);
                command.Parameters.AddWithValue("@ultimo", DbValue(snapshot.Ultimo));
                command.Parameters.AddWithValue("@volume", DbValue(snapshot.Volume));
                command.Parameters.AddWithValue("@negocios", DbValue(snapshot.Negocios));
                command.Parameters.AddWithValue("@raw_json", JsonHelper.Serialize(snapshot.ToLiveMessage()));
                command.Parameters.AddWithValue("@updated_at", timestamp.ToString("o"));
                command.ExecuteNonQuery();
            }
        }

        private static void AddCommonParameters(SQLiteCommand command, MarketSnapshot snapshot)
        {
            command.Parameters.AddWithValue("@local_timestamp", snapshot.LocalTimestamp.ToString("o"));
            command.Parameters.AddWithValue("@asset", snapshot.Asset ?? string.Empty);
            command.Parameters.AddWithValue("@status", snapshot.Status ?? string.Empty);
            command.Parameters.AddWithValue("@abertura", DbValue(snapshot.Abertura));
            command.Parameters.AddWithValue("@maxima", DbValue(snapshot.Maxima));
            command.Parameters.AddWithValue("@minima", DbValue(snapshot.Minima));
            command.Parameters.AddWithValue("@ultimo", DbValue(snapshot.Ultimo));
            command.Parameters.AddWithValue("@media", DbValue(snapshot.Media));
            command.Parameters.AddWithValue("@volume", DbValue(snapshot.Volume));
            command.Parameters.AddWithValue("@quantidade", DbValue(snapshot.Quantidade));
            command.Parameters.AddWithValue("@negocios", DbValue(snapshot.Negocios));
            command.Parameters.AddWithValue("@oferta_compra", DbValue(snapshot.OfertaCompra));
            command.Parameters.AddWithValue("@oferta_venda", DbValue(snapshot.OfertaVenda));
            command.Parameters.AddWithValue("@volume_oferta_compra", DbValue(snapshot.VolumeOfertaCompra));
            command.Parameters.AddWithValue("@volume_oferta_venda", DbValue(snapshot.VolumeOfertaVenda));
            command.Parameters.AddWithValue("@volume_projetado", DbValue(snapshot.VolumeProjetado));
            command.Parameters.AddWithValue("@raw_json", JsonHelper.Serialize(snapshot.ToLiveMessage()));
        }

        private static object DbValue(decimal? value)
        {
            return value.HasValue ? (object)value.Value : DBNull.Value;
        }

        private static void Execute(SQLiteConnection connection, string sql)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        private void EnsureDatabaseDirectory()
        {
            string marker = "Data Source=";
            string connectionString = _config.ConnectionString ?? string.Empty;
            int start = connectionString.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (start < 0)
            {
                return;
            }

            start += marker.Length;
            int end = connectionString.IndexOf(';', start);
            string path = end >= 0 ? connectionString.Substring(start, end - start) : connectionString.Substring(start);
            path = path.Trim();

            if (string.IsNullOrWhiteSpace(path) || path.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
