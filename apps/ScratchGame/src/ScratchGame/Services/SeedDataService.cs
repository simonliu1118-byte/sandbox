using ScratchGame.Data;

namespace ScratchGame.Services;

public sealed class SeedDataService(AppDatabase database)
{
    public async Task EnsureSeedDataAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var userCount = connection.CreateCommand();
        userCount.Transaction = transaction;
        userCount.CommandText = "SELECT COUNT(*) FROM users;";
        if (Convert.ToInt64(await userCount.ExecuteScalarAsync(cancellationToken)) == 0)
        {
            var addUser = connection.CreateCommand();
            addUser.Transaction = transaction;
            addUser.CommandText = """
                INSERT INTO users(id, display_name, total_spent, total_redeemed, created_utc)
                VALUES($id, '玩家 1', 0, 0, $createdUtc);
                """;
            addUser.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
            addUser.Parameters.AddWithValue("$createdUtc", DateTimeOffset.UtcNow.ToString("O"));
            await addUser.ExecuteNonQueryAsync(cancellationToken);
        }

        await EnsureLegacyTicketAsync(connection, transaction, cancellationToken);
        await EnsureStarLine500TicketAsync(connection, transaction, cancellationToken);
        await EnsureStarLinePrizeTestTicketAsync(connection, transaction, cancellationToken);
        await EnsureTicketMetadataAsync(connection, transaction, cancellationToken);

        // 啟用／停用完全尊重使用者在設定頁的選擇；Seed 不再每次啟動強制停用舊票。

        transaction.Commit();
    }

    private static async Task EnsureLegacyTicketAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string ticketId = "builtin-three-line-100";
        if (await TicketExistsAsync(connection, transaction, ticketId, cancellationToken))
            return;

        const long issueSize = 10_000;
        var tiers = new (string Id, long Amount, long Count, int Order)[]
        {
            ("jackpot", 1_000_000, 1, 0),
            ("p10000", 10_000, 9, 1),
            ("p1000", 1_000, 100, 2),
            ("p500", 500, 400, 3),
            ("p200", 200, 1_000, 4),
            ("p100", 100, 2_000, 5),
            ("lose", 0, 6_490, 6)
        };
        var winning = tiers.Where(t => t.Amount > 0).Sum(t => t.Count);
        var winRate = (double)winning / issueSize;
        await InsertTicketAsync(
            connection, transaction,
            ticketId, "三星連線", 100, "ThreeLine", issueSize, winRate,
            styleNumber: 0, ticketsPerBook: 100, priceDisplay: 0,
            tiers, cancellationToken);
    }

    private static async Task EnsureStarLine500TicketAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string ticketId = "builtin-star-line-500-v1";
        if (await TicketExistsAsync(connection, transaction, ticketId, cancellationToken))
            return;

        const long issueSize = 10_000;
        var tiers = new (string Id, long Amount, long Count, int Order)[]
        {
            ("line8", 100_000, 1, 0),
            ("line6", 10_000, 9, 1),
            ("line5", 5_000, 40, 2),
            ("line4", 2_500, 200, 3),
            ("line3", 1_000, 750, 4),
            ("line2", 500, 3_500, 5),
            ("line1", 100, 5_500, 6)
        };
        await InsertTicketAsync(
            connection, transaction,
            ticketId, "三星連線", 500, "1", issueSize, 1.0,
            styleNumber: 1, ticketsPerBook: 100, priceDisplay: 1,
            tiers, cancellationToken);
    }

    private static async Task EnsureStarLinePrizeTestTicketAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string ticketId = "builtin-star-line-500-prize-test";
        if (await TicketExistsAsync(connection, transaction, ticketId, cancellationToken))
            return;

        // 開發測試票：每個合法獎項各 1 張，方便一次測到二獎／頭獎效果。
        var tiers = new (string Id, long Amount, long Count, int Order)[]
        {
            ("line8", 100_000, 1, 0),
            ("line6", 10_000, 1, 1),
            ("line5", 5_000, 1, 2),
            ("line4", 2_500, 1, 3),
            ("line3", 1_000, 1, 4),
            ("line2", 500, 1, 5),
            ("line1", 100, 1, 6)
        };
        var styleNumber = await GetNextFreeStyleNumberAsync(connection, transaction, cancellationToken);
        await InsertTicketAsync(
            connection, transaction,
            ticketId, "三星連線（獎項測試）", 500, "1", 7, 1.0,
            styleNumber, ticketsPerBook: 7, priceDisplay: 1,
            tiers, cancellationToken);
    }

    private static async Task<bool> TicketExistsAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        string ticketId,
        CancellationToken cancellationToken)
    {
        var exists = connection.CreateCommand();
        exists.Transaction = transaction;
        exists.CommandText = "SELECT 1 FROM ticket_definitions WHERE id = $id LIMIT 1;";
        exists.Parameters.AddWithValue("$id", ticketId);
        return await exists.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<long> GetNextFreeStyleNumberAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var used = new HashSet<long>();
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT style_number FROM ticket_metadata WHERE style_number > 0 ORDER BY style_number;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            used.Add(reader.GetInt64(0));

        long candidate = 1;
        while (used.Contains(candidate))
            candidate++;
        return candidate;
    }

    private static async Task EnsureTicketMetadataAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var builtIns = new[]
        {
            (Id: "builtin-three-line-100", Style: 0L, PerBook: 100L, PriceDisplay: 0),
            (Id: "builtin-star-line-500-v1", Style: 1L, PerBook: 100L, PriceDisplay: 1)
        };

        foreach (var item in builtIns)
        {
            var upsert = connection.CreateCommand();
            upsert.Transaction = transaction;
            upsert.CommandText = """
                INSERT INTO ticket_metadata(ticket_id, style_number, tickets_per_book, price_display)
                SELECT id, $style, $perBook, $priceDisplay
                FROM ticket_definitions
                WHERE id = $id
                ON CONFLICT(ticket_id) DO UPDATE SET
                    style_number = excluded.style_number,
                    tickets_per_book = excluded.tickets_per_book,
                    price_display = excluded.price_display;
                """;
            upsert.Parameters.AddWithValue("$id", item.Id);
            upsert.Parameters.AddWithValue("$style", item.Style);
            upsert.Parameters.AddWithValue("$perBook", item.PerBook);
            upsert.Parameters.AddWithValue("$priceDisplay", item.PriceDisplay);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        var fallback = connection.CreateCommand();
        fallback.Transaction = transaction;
        fallback.CommandText = """
            INSERT OR IGNORE INTO ticket_metadata(ticket_id, style_number, tickets_per_book, price_display)
            SELECT id, 0, issue_size, 0
            FROM ticket_definitions;
            """;
        await fallback.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertTicketAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        string ticketId,
        string displayName,
        long price,
        string ruleId,
        long issueSize,
        double winRate,
        long styleNumber,
        long ticketsPerBook,
        int priceDisplay,
        IReadOnlyList<(string Id, long Amount, long Count, int Order)> tiers,
        CancellationToken cancellationToken)
    {
        if (tiers.Sum(t => t.Count) != issueSize)
            throw new InvalidOperationException($"{displayName} 的獎項張數合計與發行張數不一致。");
        if (ticketsPerBook <= 0 || issueSize % ticketsPerBook != 0)
            throw new InvalidOperationException($"{displayName} 的總發行張數必須能被每本張數整除。");

        var addTicket = connection.CreateCommand();
        addTicket.Transaction = transaction;
        addTicket.CommandText = """
            INSERT INTO ticket_definitions(
                id, display_name, price, rule_id, issue_size,
                published_win_rate, enabled, locked, source_package_id, created_utc)
            VALUES($id, $displayName, $price, $ruleId, $issueSize,
                   $winRate, 1, 1, NULL, $createdUtc);
            """;
        addTicket.Parameters.AddWithValue("$id", ticketId);
        addTicket.Parameters.AddWithValue("$displayName", displayName);
        addTicket.Parameters.AddWithValue("$price", price);
        addTicket.Parameters.AddWithValue("$ruleId", ruleId);
        addTicket.Parameters.AddWithValue("$issueSize", issueSize);
        addTicket.Parameters.AddWithValue("$winRate", winRate);
        addTicket.Parameters.AddWithValue("$createdUtc", DateTimeOffset.UtcNow.ToString("O"));
        await addTicket.ExecuteNonQueryAsync(cancellationToken);

        var metadata = connection.CreateCommand();
        metadata.Transaction = transaction;
        metadata.CommandText = """
            INSERT INTO ticket_metadata(ticket_id, style_number, tickets_per_book, price_display)
            VALUES($ticketId, $styleNumber, $ticketsPerBook, $priceDisplay);
            """;
        metadata.Parameters.AddWithValue("$ticketId", ticketId);
        metadata.Parameters.AddWithValue("$styleNumber", styleNumber);
        metadata.Parameters.AddWithValue("$ticketsPerBook", ticketsPerBook);
        metadata.Parameters.AddWithValue("$priceDisplay", priceDisplay);
        await metadata.ExecuteNonQueryAsync(cancellationToken);

        foreach (var tier in tiers)
        {
            var addTier = connection.CreateCommand();
            addTier.Transaction = transaction;
            addTier.CommandText = """
                INSERT INTO prize_tiers(ticket_id, tier_id, amount, initial_count, sort_order)
                VALUES($ticketId, $tierId, $amount, $count, $sortOrder);
                """;
            addTier.Parameters.AddWithValue("$ticketId", ticketId);
            addTier.Parameters.AddWithValue("$tierId", tier.Id);
            addTier.Parameters.AddWithValue("$amount", tier.Amount);
            addTier.Parameters.AddWithValue("$count", tier.Count);
            addTier.Parameters.AddWithValue("$sortOrder", tier.Order);
            await addTier.ExecuteNonQueryAsync(cancellationToken);
        }

        var batchId = Guid.NewGuid().ToString("D");
        var addBatch = connection.CreateCommand();
        addBatch.Transaction = transaction;
        addBatch.CommandText = """
            INSERT INTO batches(id, ticket_id, batch_number, status, started_utc)
            VALUES($id, $ticketId, 1, 'Active', $startedUtc);
            """;
        addBatch.Parameters.AddWithValue("$id", batchId);
        addBatch.Parameters.AddWithValue("$ticketId", ticketId);
        addBatch.Parameters.AddWithValue("$startedUtc", DateTimeOffset.UtcNow.ToString("O"));
        await addBatch.ExecuteNonQueryAsync(cancellationToken);

        foreach (var tier in tiers)
        {
            var state = connection.CreateCommand();
            state.Transaction = transaction;
            state.CommandText = """
                INSERT INTO batch_prize_state(
                    batch_id, tier_id, amount,
                    available_count, reserved_count, consumed_count)
                VALUES($batchId, $tierId, $amount, $count, 0, 0);
                """;
            state.Parameters.AddWithValue("$batchId", batchId);
            state.Parameters.AddWithValue("$tierId", tier.Id);
            state.Parameters.AddWithValue("$amount", tier.Amount);
            state.Parameters.AddWithValue("$count", tier.Count);
            await state.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
