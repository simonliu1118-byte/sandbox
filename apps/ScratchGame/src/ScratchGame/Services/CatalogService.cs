using ScratchGame.Data;
using ScratchGame.Models;

namespace ScratchGame.Services;

public sealed class CatalogService(AppDatabase database)
{
    public async Task<IReadOnlyList<UserProfile>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<UserProfile>();
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, display_name,
                   total_spent, total_redeemed,
                   wallet_balance, completed_ticket_count, win_count, max_prize,
                   grant_count, grant_total_amount
            FROM users
            ORDER BY created_utc, display_name;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new UserProfile(
                reader.GetString(0), reader.GetString(1),
                reader.GetInt64(2), reader.GetInt64(3),
                reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7),
                reader.GetInt64(8), reader.GetInt64(9)));
        }
        return result;
    }

    public async Task<UserProfile> CreateUserAsync(
        string displayName,
        CancellationToken cancellationToken = default)
    {
        displayName = displayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("使用者名稱不可空白。", nameof(displayName));
        if (displayName.Length > 30)
            throw new ArgumentException("使用者名稱不可超過 30 個字元。", nameof(displayName));

        var id = Guid.NewGuid().ToString("D");
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users(
                id, display_name,
                total_spent, total_redeemed,
                wallet_balance, completed_ticket_count, win_count, max_prize,
                grant_count, grant_total_amount,
                created_utc)
            VALUES(
                $id, $name,
                0, 0,
                $wallet, 0, 0, 0,
                0, 0,
                $createdUtc);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$name", displayName);
        command.Parameters.AddWithValue("$wallet", AppDatabase.InitialWalletBalance);
        command.Parameters.AddWithValue("$createdUtc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new UserProfile(
            id,
            displayName,
            0,
            0,
            AppDatabase.InitialWalletBalance,
            0,
            0,
            0,
            0,
            0);
    }

    public async Task<IReadOnlyList<TicketDefinition>> GetAvailableTicketsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new List<TicketDefinition>();
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.id, t.display_name, t.price, t.rule_id, t.issue_size,
                   t.published_win_rate, t.enabled, t.locked, t.source_package_id,
                   COALESCE((
                       SELECT MAX(b.batch_number)
                       FROM batches b
                       WHERE b.ticket_id = t.id
                         AND b.status = 'Active'
                   ), 0) AS active_batch_number
            FROM ticket_definitions t
            WHERE t.enabled = 1
              AND EXISTS (
                  SELECT 1
                  FROM batches b
                  JOIN batch_prize_state s ON s.batch_id = b.id
                  WHERE b.ticket_id = t.id
                    AND b.status = 'Active'
                    AND s.available_count > 0
              )
            ORDER BY t.price, t.display_name;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadTicketDefinition(reader));
        return result;
    }

    public async Task<TicketDefinition?> GetTicketByIdAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.id, t.display_name, t.price, t.rule_id, t.issue_size,
                   t.published_win_rate, t.enabled, t.locked, t.source_package_id,
                   COALESCE((
                       SELECT MAX(b.batch_number)
                       FROM batches b
                       WHERE b.ticket_id = t.id
                         AND b.status = 'Active'
                   ), 0) AS active_batch_number
            FROM ticket_definitions t
            WHERE t.id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", ticketId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadTicketDefinition(reader) : null;
    }

    public async Task<bool> HasRemainingTicketsAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1
                FROM ticket_definitions t
                JOIN batches b ON b.ticket_id = t.id
                JOIN batch_prize_state s ON s.batch_id = b.id
                WHERE t.id = $ticketId
                  AND t.enabled = 1
                  AND b.status = 'Active'
                  AND s.available_count > 0
            );
            """;
        command.Parameters.AddWithValue("$ticketId", ticketId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 0;
    }

    private static TicketDefinition ReadTicketDefinition(Microsoft.Data.Sqlite.SqliteDataReader reader)
        => new(
            reader.GetString(0), reader.GetString(1), reader.GetInt64(2),
            reader.GetString(3), reader.GetInt64(4), reader.GetDouble(5),
            reader.GetInt64(6) != 0, reader.GetInt64(7) != 0,
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.FieldCount > 9 && !reader.IsDBNull(9) ? reader.GetInt32(9) : 0);

    public async Task<PendingTicket?> GetPendingForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, user_id, ticket_id, batch_id, reserved_tier_id,
                   reserved_amount, price, payload_json, created_utc, scratch_state_json
            FROM pending_tickets
            WHERE user_id = $userId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$userId", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new PendingTicket(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetString(7),
            DateTimeOffset.Parse(reader.GetString(8)),
            reader.IsDBNull(9) ? null : reader.GetString(9));
    }

    public async Task<int> EnsureInitialBatchAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(MAX(batch_number), 0)
            FROM batches
            WHERE ticket_id = $ticketId;
            """;
        command.Parameters.AddWithValue("$ticketId", ticketId);
        var currentMax = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (currentMax > 0)
            return currentMax;

        return await StartNextBatchAsync(ticketId, cancellationToken);
    }

    public async Task<int> StartNextBatchAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var active = connection.CreateCommand();
        active.Transaction = transaction;
        active.CommandText = """
            SELECT id, batch_number
            FROM batches
            WHERE ticket_id = $ticketId AND status = 'Active'
            LIMIT 1;
            """;
        active.Parameters.AddWithValue("$ticketId", ticketId);

        string? oldBatchId = null;
        var oldBatchNumber = 0;
        await using (var reader = await active.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                oldBatchId = reader.GetString(0);
                oldBatchNumber = reader.GetInt32(1);
            }
        }

        if (oldBatchId is not null)
        {
            var pending = connection.CreateCommand();
            pending.Transaction = transaction;
            pending.CommandText = "SELECT COUNT(*) FROM pending_tickets WHERE batch_id = $batchId;";
            pending.Parameters.AddWithValue("$batchId", oldBatchId);
            if (Convert.ToInt64(await pending.ExecuteScalarAsync(cancellationToken)) > 0)
                throw new InvalidOperationException("目前批次仍有尚未完成的彩券，請先直接開獎並完成結算。");

            var close = connection.CreateCommand();
            close.Transaction = transaction;
            close.CommandText = """
                UPDATE batches
                SET status = 'Closed',
                    closed_utc = $closedUtc,
                    consumed_count = (
                        SELECT COALESCE(SUM(consumed_count), 0)
                        FROM batch_prize_state
                        WHERE batch_id = $batchId
                    )
                WHERE id = $batchId;
                """;
            close.Parameters.AddWithValue("$closedUtc", DateTimeOffset.UtcNow.ToString("O"));
            close.Parameters.AddWithValue("$batchId", oldBatchId);
            await close.ExecuteNonQueryAsync(cancellationToken);

            var clearPool = connection.CreateCommand();
            clearPool.Transaction = transaction;
            clearPool.CommandText = "DELETE FROM batch_prize_state WHERE batch_id = $batchId;";
            clearPool.Parameters.AddWithValue("$batchId", oldBatchId);
            await clearPool.ExecuteNonQueryAsync(cancellationToken);
        }

        var nextNumber = oldBatchNumber + 1;
        if (nextNumber <= 0)
        {
            var max = connection.CreateCommand();
            max.Transaction = transaction;
            max.CommandText = "SELECT COALESCE(MAX(batch_number), 0) FROM batches WHERE ticket_id = $ticketId;";
            max.Parameters.AddWithValue("$ticketId", ticketId);
            nextNumber = Convert.ToInt32(await max.ExecuteScalarAsync(cancellationToken)) + 1;
        }

        var newBatchId = Guid.NewGuid().ToString("D");
        var addBatch = connection.CreateCommand();
        addBatch.Transaction = transaction;
        addBatch.CommandText = """
            INSERT INTO batches(id, ticket_id, batch_number, status, started_utc)
            VALUES($id, $ticketId, $batchNumber, 'Active', $startedUtc);
            """;
        addBatch.Parameters.AddWithValue("$id", newBatchId);
        addBatch.Parameters.AddWithValue("$ticketId", ticketId);
        addBatch.Parameters.AddWithValue("$batchNumber", nextNumber);
        addBatch.Parameters.AddWithValue("$startedUtc", DateTimeOffset.UtcNow.ToString("O"));
        await addBatch.ExecuteNonQueryAsync(cancellationToken);

        var resetPool = connection.CreateCommand();
        resetPool.Transaction = transaction;
        resetPool.CommandText = """
            INSERT INTO batch_prize_state(
                batch_id, tier_id, amount,
                available_count, reserved_count, consumed_count)
            SELECT $batchId, tier_id, amount, initial_count, 0, 0
            FROM prize_tiers
            WHERE ticket_id = $ticketId;
            """;
        resetPool.Parameters.AddWithValue("$batchId", newBatchId);
        resetPool.Parameters.AddWithValue("$ticketId", ticketId);
        if (await resetPool.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new InvalidOperationException("此彩券沒有有效獎項表，不能建立新批次。");

        var lockTicket = connection.CreateCommand();
        lockTicket.Transaction = transaction;
        lockTicket.CommandText = "UPDATE ticket_definitions SET locked = 1 WHERE id = $ticketId;";
        lockTicket.Parameters.AddWithValue("$ticketId", ticketId);
        await lockTicket.ExecuteNonQueryAsync(cancellationToken);

        transaction.Commit();
        return nextNumber;
    }
}
