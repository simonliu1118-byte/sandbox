using Microsoft.Data.Sqlite;

namespace ScratchGame.Data;

public sealed class AppDatabase
{
    public string DataDirectory { get; }
    public string DatabasePath { get; }
    public string BackupDirectory { get; }

    public AppDatabase()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScratchGame");
        DatabasePath = Path.Combine(DataDirectory, "scratchgame.db");
        BackupDirectory = Path.Combine(DataDirectory, "backup");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);

        var existedBeforeOpen = File.Exists(DatabasePath) && new FileInfo(DatabasePath).Length > 0;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var previousSchemaVersion = existedBeforeOpen
            ? await GetSchemaVersionAsync(connection, cancellationToken)
            : 0;

        if (existedBeforeOpen && previousSchemaVersion < 4)
            await CreateMigrationBackupAsync(connection, cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS app_meta (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS users (
                id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                total_spent INTEGER NOT NULL DEFAULT 0,
                total_redeemed INTEGER NOT NULL DEFAULT 0,
                created_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ticket_definitions (
                id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                price INTEGER NOT NULL CHECK(price > 0),
                rule_id TEXT NOT NULL,
                issue_size INTEGER NOT NULL CHECK(issue_size > 0),
                published_win_rate REAL NOT NULL CHECK(published_win_rate >= 0 AND published_win_rate <= 1),
                enabled INTEGER NOT NULL DEFAULT 1,
                locked INTEGER NOT NULL DEFAULT 0,
                source_package_id TEXT NULL,
                created_utc TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_ticket_package_id
                ON ticket_definitions(source_package_id)
                WHERE source_package_id IS NOT NULL;

            CREATE TABLE IF NOT EXISTS scratchpack_installations (
                package_id TEXT PRIMARY KEY,
                ticket_id TEXT NOT NULL UNIQUE,
                source_kind TEXT NOT NULL CHECK(source_kind IN ('BuiltIn','Imported')),
                content_hash TEXT NOT NULL,
                installed_utc TEXT NOT NULL,
                FOREIGN KEY(ticket_id) REFERENCES ticket_definitions(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS ticket_metadata (
                ticket_id TEXT PRIMARY KEY,
                style_number INTEGER NOT NULL DEFAULT 0 CHECK(style_number >= 0),
                tickets_per_book INTEGER NOT NULL CHECK(tickets_per_book > 0),
                price_display INTEGER NOT NULL DEFAULT 0 CHECK(price_display IN (0, 1)),
                FOREIGN KEY(ticket_id) REFERENCES ticket_definitions(id) ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_ticket_style_number
                ON ticket_metadata(style_number)
                WHERE style_number > 0;

            CREATE TABLE IF NOT EXISTS prize_tiers (
                ticket_id TEXT NOT NULL,
                tier_id TEXT NOT NULL,
                amount INTEGER NOT NULL CHECK(amount >= 0),
                initial_count INTEGER NOT NULL CHECK(initial_count > 0),
                sort_order INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY(ticket_id, tier_id),
                FOREIGN KEY(ticket_id) REFERENCES ticket_definitions(id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS batches (
                id TEXT PRIMARY KEY,
                ticket_id TEXT NOT NULL,
                batch_number INTEGER NOT NULL CHECK(batch_number > 0),
                status TEXT NOT NULL CHECK(status IN ('Active','Closed')),
                started_utc TEXT NOT NULL,
                closed_utc TEXT NULL,
                consumed_count INTEGER NOT NULL DEFAULT 0 CHECK(consumed_count >= 0),
                UNIQUE(ticket_id, batch_number),
                FOREIGN KEY(ticket_id) REFERENCES ticket_definitions(id) ON DELETE RESTRICT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_one_active_batch_per_ticket
                ON batches(ticket_id)
                WHERE status = 'Active';

            CREATE TABLE IF NOT EXISTS batch_prize_state (
                batch_id TEXT NOT NULL,
                tier_id TEXT NOT NULL,
                amount INTEGER NOT NULL CHECK(amount >= 0),
                available_count INTEGER NOT NULL CHECK(available_count >= 0),
                reserved_count INTEGER NOT NULL CHECK(reserved_count >= 0),
                consumed_count INTEGER NOT NULL CHECK(consumed_count >= 0),
                PRIMARY KEY(batch_id, tier_id),
                FOREIGN KEY(batch_id) REFERENCES batches(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS pending_tickets (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL UNIQUE,
                ticket_id TEXT NOT NULL,
                batch_id TEXT NOT NULL,
                reserved_tier_id TEXT NOT NULL,
                reserved_amount INTEGER NOT NULL CHECK(reserved_amount >= 0),
                price INTEGER NOT NULL CHECK(price > 0),
                payload_json TEXT NOT NULL,
                scratch_state_json TEXT NULL,
                created_utc TEXT NOT NULL,
                FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE RESTRICT,
                FOREIGN KEY(ticket_id) REFERENCES ticket_definitions(id) ON DELETE RESTRICT,
                FOREIGN KEY(batch_id) REFERENCES batches(id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS ix_pending_batch ON pending_tickets(batch_id);

            CREATE TABLE IF NOT EXISTS batch_serial_claims (
                batch_id TEXT NOT NULL,
                serial_index INTEGER NOT NULL CHECK(serial_index > 0),
                pending_ticket_id TEXT NULL,
                consumed INTEGER NOT NULL DEFAULT 0 CHECK(consumed IN (0, 1)),
                PRIMARY KEY(batch_id, serial_index),
                UNIQUE(pending_ticket_id),
                FOREIGN KEY(batch_id) REFERENCES batches(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_serial_pending
                ON batch_serial_claims(pending_ticket_id);

            CREATE TABLE IF NOT EXISTS ticket_history (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL,
                ticket_id TEXT NOT NULL,
                batch_number INTEGER NOT NULL,
                price INTEGER NOT NULL,
                prize_amount INTEGER NOT NULL CHECK(prize_amount >= 0),
                completed_utc TEXT NOT NULL,
                FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE RESTRICT,
                FOREIGN KEY(ticket_id) REFERENCES ticket_definitions(id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS ix_history_user_time
                ON ticket_history(user_id, completed_utc DESC);

            -- Schema 3 起不再使用 Reservation。舊 Pending 的 reserved 數量視為已經發行，
            -- 轉入 consumed_count；之後 reserved_count 永遠維持 0，只為舊資料庫相容而保留欄位。
            UPDATE batch_prize_state
            SET consumed_count = consumed_count + reserved_count,
                reserved_count = 0
            WHERE reserved_count > 0;

            INSERT INTO app_meta(key, value)
            VALUES ('schema_version', '4')
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection($"Data Source={DatabasePath};Cache=Shared");
        await connection.OpenAsync(cancellationToken);

        var pragma = connection.CreateCommand();
        pragma.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA busy_timeout = 5000;
            """;
        await pragma.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task<int> GetSchemaVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "app_meta", cancellationToken))
            return 0;

        var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_meta WHERE key = 'schema_version' LIMIT 1;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null && int.TryParse(Convert.ToString(value), out var version) ? version : 0;
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", tableName);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private async Task CreateMigrationBackupAsync(
        SqliteConnection source,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(BackupDirectory);
        var path = Path.Combine(
            BackupDirectory,
            $"ScratchGame_migration_{DateTime.Now:yyyyMMdd-HHmmss}.db");

        await using var destination = new SqliteConnection($"Data Source={path}");
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);

        var backups = Directory.EnumerateFiles(BackupDirectory, "ScratchGame_*.db")
            .Select(file => new FileInfo(file))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();
        foreach (var old in backups.Skip(5))
        {
            try { old.Delete(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
