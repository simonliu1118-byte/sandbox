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

        await using var connection = await OpenConnectionAsync(cancellationToken);
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

            INSERT INTO app_meta(key, value)
            VALUES ('schema_version', '1')
            ON CONFLICT(key) DO NOTHING;
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
}
