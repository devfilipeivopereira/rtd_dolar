using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Threading;
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
        private int _saving;
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
            }

            _initialized = true;
            _log.Info("SQLite inicializado.");
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
