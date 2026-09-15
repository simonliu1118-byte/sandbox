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

        // 購票與扣款是同一個交易。錢包不足時整個交易回滾，票池不會被吃掉。
        var charge = connection.CreateCommand();
        charge.Transaction = transaction;
        charge.CommandText = """
            UPDATE users
            SET wallet_balance = wallet_balance - $price,
                total_spent = total_spent + $price
            WHERE id = $userId
              AND wallet_balance >= $price;
            """;
        charge.Parameters.AddWithValue("$price", ticket.Price);
        charge.Parameters.AddWithValue("$userId", userId);
        if (await charge.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            var exists = connection.CreateCommand();
            exists.Transaction = transaction;
            exists.CommandText = "SELECT EXISTS(SELECT 1 FROM users WHERE id = $userId);";
            exists.Parameters.AddWithValue("$userId", userId);
            if (Convert.ToInt64(await exists.ExecuteScalarAsync(cancellationToken)) == 0)
                throw new InvalidOperationException("找不到目前使用者。");

            throw new InvalidOperationException($"錢包餘額不足，這張彩券需要 ${ticket.Price:N0}。");
        }

        // Schema 3 起：抽出票即視為已發行，直接從 Remaining 扣除。
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

        // 欄位名稱 reserved_* 為既有資料庫相容名稱；Schema 3 起語意是
        //「這張已發行票的既定獎項」。
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

        // V0.4 不再保存逐張玩家歷史。完成一張票時直接更新不可逆的累積統計。
        var settle = connection.CreateCommand();
        settle.Transaction = transaction;
        settle.CommandText = """
            UPDATE users
            SET wallet_balance = wallet_balance + $amount,
                total_redeemed = total_redeemed + $amount,
                completed_ticket_count = completed_ticket_count + 1,
                win_count = win_count + CASE WHEN $amount > 0 THEN 1 ELSE 0 END,
                max_prize = CASE WHEN $amount > max_prize THEN $amount ELSE max_prize END
            WHERE id = $userId;
            """;
        settle.Parameters.AddWithValue("$amount", pending.ReservedAmount);
        settle.Parameters.AddWithValue("$userId", pending.UserId);
        if (await settle.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("找不到 Pending Ticket 所屬使用者。");

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
            SELECT p.id, p.user_id, p.reserved_amount
            FROM pending_tickets p
            WHERE p.id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", pendingTicketId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("找不到尚未完成的彩券。");

        return new PendingSettlement(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt64(2));
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
        long ReservedAmount);
}
