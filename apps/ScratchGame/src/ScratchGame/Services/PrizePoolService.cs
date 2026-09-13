using Microsoft.Data.Sqlite;
using ScratchGame.Data;
using ScratchGame.Models;

namespace ScratchGame.Services;

public sealed class PrizePoolService(AppDatabase database)
{
    public async Task<PendingTicket> CreatePendingAsync(
        string userId,
        string ticketId,
        Func<long, string> payloadFactory,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        if (await HasPendingAsync(connection, transaction, userId, cancellationToken))
            throw new InvalidOperationException("此使用者已有尚未完成的彩券。");

        var ticket = await GetTicketAndActiveBatchAsync(
            connection, transaction, ticketId, cancellationToken);

        var tiers = await GetAvailableTiersAsync(
            connection, transaction, ticket.BatchId, cancellationToken);
        if (tiers.Count == 0)
            throw new InvalidOperationException("目前批次已售罄，請手動發行新一批。");

        var selected = DrawWeighted(tiers);
        var payloadJson = payloadFactory(selected.Amount);
        var pendingId = Guid.NewGuid().ToString("D");
        var createdAt = DateTimeOffset.UtcNow;

        // Schema 3：抽出票時即視為已發行，直接從 Remaining 扣除。
        // consumed_count 在資料庫內代表「已發行張數（含尚未刮完與已完成）」；不再建立 Reservation。
        var issue = connection.CreateCommand();
        issue.Transaction = transaction;
        issue.CommandText = """
            UPDATE batch_prize_state
            SET available_count = available_count - 1,
                consumed_count = consumed_count + 1
            WHERE batch_id = $batchId
              AND tier_id = $tierId
              AND available_count > 0;
            """;
        issue.Parameters.AddWithValue("$batchId", ticket.BatchId);
        issue.Parameters.AddWithValue("$tierId", selected.TierId);
        if (await issue.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("獎池已被其他操作更新，請重新抽票。");

        var charge = connection.CreateCommand();
        charge.Transaction = transaction;
        charge.CommandText = """
            UPDATE users
            SET total_spent = total_spent + $price
            WHERE id = $userId;
            """;
        charge.Parameters.AddWithValue("$price", ticket.Price);
        charge.Parameters.AddWithValue("$userId", userId);
        if (await charge.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("找不到目前使用者。");

        // 欄位名稱 reserved_* 為既有資料庫相容名稱；Schema 3 起語意是「這張已發行票的既定獎項」。
        var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO pending_tickets(
                id, user_id, ticket_id, batch_id,
                reserved_tier_id, reserved_amount, price,
                payload_json, scratch_state_json, created_utc)
            VALUES(
                $id, $userId, $ticketId, $batchId,
                $tierId, $amount, $price,
                $payload, NULL, $createdUtc);
            """;
        insert.Parameters.AddWithValue("$id", pendingId);
        insert.Parameters.AddWithValue("$userId", userId);
        insert.Parameters.AddWithValue("$ticketId", ticketId);
        insert.Parameters.AddWithValue("$batchId", ticket.BatchId);
        insert.Parameters.AddWithValue("$tierId", selected.TierId);
        insert.Parameters.AddWithValue("$amount", selected.Amount);
        insert.Parameters.AddWithValue("$price", ticket.Price);
        insert.Parameters.AddWithValue("$payload", payloadJson);
        insert.Parameters.AddWithValue("$createdUtc", createdAt.ToString("O"));
        await insert.ExecuteNonQueryAsync(cancellationToken);

        transaction.Commit();

        return new PendingTicket(
            pendingId,
            userId,
            ticketId,
            ticket.BatchId,
            selected.TierId,
            selected.Amount,
            ticket.Price,
            payloadJson,
            createdAt);
    }

    public async Task<long> RedeemAsync(
        string pendingTicketId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var pending = await GetPendingForSettlementAsync(
            connection, transaction, pendingTicketId, cancellationToken);

        // 獎項在「發行」時已從 Remaining 扣除，因此兌獎不再修改票池，只完成使用者與歷史資料。
        if (pending.ReservedAmount > 0)
        {
            var credit = connection.CreateCommand();
            credit.Transaction = transaction;
            credit.CommandText = """
                UPDATE users
                SET total_redeemed = total_redeemed + $amount
                WHERE id = $userId;
                """;
            credit.Parameters.AddWithValue("$amount", pending.ReservedAmount);
            credit.Parameters.AddWithValue("$userId", pending.UserId);
            if (await credit.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("找不到 Pending Ticket 所屬使用者。");
        }

        await InsertHistoryAsync(
            connection, transaction, pending, pending.ReservedAmount, cancellationToken);

        // 票號一旦實際發行並完成結算就永久標記已使用，不能因 Pending 被刪除後再次釋出。
        var consumeSerial = connection.CreateCommand();
        consumeSerial.Transaction = transaction;
        consumeSerial.CommandText = "UPDATE batch_serial_claims SET consumed = 1 WHERE pending_ticket_id = $pendingId;";
        consumeSerial.Parameters.AddWithValue("$pendingId", pending.Id);
        await consumeSerial.ExecuteNonQueryAsync(cancellationToken);

        await DeletePendingAsync(connection, transaction, pending.Id, cancellationToken);

        transaction.Commit();
        return pending.ReservedAmount;
    }

    private static async Task<bool> HasPendingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string userId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM pending_tickets WHERE user_id = $userId LIMIT 1;";
        command.Parameters.AddWithValue("$userId", userId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<(string BatchId, long Price)> GetTicketAndActiveBatchAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string ticketId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT b.id, t.price
            FROM ticket_definitions t
            JOIN batches b ON b.ticket_id = t.id AND b.status = 'Active'
            WHERE t.id = $ticketId AND t.enabled = 1
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$ticketId", ticketId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("此彩券目前沒有可用的 Active 批次。");
        return (reader.GetString(0), reader.GetInt64(1));
    }

    private static async Task<List<AvailableTier>> GetAvailableTiersAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string batchId,
        CancellationToken cancellationToken)
    {
        var result = new List<AvailableTier>();
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT tier_id, amount, available_count
            FROM batch_prize_state
            WHERE batch_id = $batchId AND available_count > 0;
            """;
        command.Parameters.AddWithValue("$batchId", batchId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new AvailableTier(reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2)));
        return result;
    }

    private static AvailableTier DrawWeighted(IReadOnlyList<AvailableTier> tiers)
    {
        var total = tiers.Sum(t => t.AvailableCount);
        if (total <= 0)
            throw new InvalidOperationException("票池已售罄。");

        var draw = Random.Shared.NextInt64(total);
        long cursor = 0;
        foreach (var tier in tiers)
        {
            cursor += tier.AvailableCount;
            if (draw < cursor)
                return tier;
        }
        return tiers[^1];
    }

    private static async Task<PendingSettlement> GetPendingForSettlementAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string pendingTicketId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT p.id, p.user_id, p.ticket_id, p.batch_id,
                   p.reserved_tier_id, p.reserved_amount, p.price,
                   b.batch_number
            FROM pending_tickets p
            JOIN batches b ON b.id = p.batch_id
            WHERE p.id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", pendingTicketId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("找不到尚未完成的彩券。");

        return new PendingSettlement(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetInt32(7));
    }

    private static async Task InsertHistoryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PendingSettlement pending,
        long prizeAmount,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ticket_history(
                id, user_id, ticket_id, batch_number,
                price, prize_amount, completed_utc)
            VALUES($id, $userId, $ticketId, $batchNumber, $price, $prize, $completedUtc);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$userId", pending.UserId);
        command.Parameters.AddWithValue("$ticketId", pending.TicketId);
        command.Parameters.AddWithValue("$batchNumber", pending.BatchNumber);
        command.Parameters.AddWithValue("$price", pending.Price);
        command.Parameters.AddWithValue("$prize", prizeAmount);
        command.Parameters.AddWithValue("$completedUtc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeletePendingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string id,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM pending_tickets WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Pending Ticket 結算失敗。");
    }

    private sealed record AvailableTier(string TierId, long Amount, long AvailableCount);
    private sealed record PendingSettlement(
        string Id,
        string UserId,
        string TicketId,
        string BatchId,
        string ReservedTierId,
        long ReservedAmount,
        long Price,
        int BatchNumber);
}
