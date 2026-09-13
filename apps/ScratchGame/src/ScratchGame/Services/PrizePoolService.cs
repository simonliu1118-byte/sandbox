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

        var reserve = connection.CreateCommand();
        reserve.Transaction = transaction;
        reserve.CommandText = """
            UPDATE batch_prize_state
            SET available_count = available_count - 1,
                reserved_count = reserved_count + 1
            WHERE batch_id = $batchId
              AND tier_id = $tierId
              AND available_count > 0;
            """;
        reserve.Parameters.AddWithValue("$batchId", ticket.BatchId);
        reserve.Parameters.AddWithValue("$tierId", selected.TierId);
        if (await reserve.ExecuteNonQueryAsync(cancellationToken) != 1)
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

        var consume = connection.CreateCommand();
        consume.Transaction = transaction;
        consume.CommandText = """
            UPDATE batch_prize_state
            SET reserved_count = reserved_count - 1,
                consumed_count = consumed_count + 1
            WHERE batch_id = $batchId
              AND tier_id = $tierId
              AND reserved_count > 0;
            """;
        consume.Parameters.AddWithValue("$batchId", pending.BatchId);
        consume.Parameters.AddWithValue("$tierId", pending.ReservedTierId);
        if (await consume.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Pending Ticket 的保留獎項狀態不一致。");

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
        await DeletePendingAsync(connection, transaction, pending.Id, cancellationToken);

        transaction.Commit();
        return pending.ReservedAmount;
    }

    public async Task AbandonAsLossAsync(
        string pendingTicketId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var pending = await GetPendingForSettlementAsync(
            connection, transaction, pendingTicketId, cancellationToken);

        if (pending.ReservedAmount == 0)
        {
            // 原本保留的就是未中獎票；直接消耗即可，不需要先釋放再重抽。
            await ConsumeReservedAsync(
                connection, transaction, pending.BatchId, pending.ReservedTierId, cancellationToken);
        }
        else
        {
            var losingTierId = await GetAvailableLosingTierIdAsync(
                connection, transaction, pending.BatchId, cancellationToken);
            if (losingTierId is null)
                throw new InvalidOperationException("目前票池已無未中獎票，不能放棄；請揭曉並正常結算。");

            var release = connection.CreateCommand();
            release.Transaction = transaction;
            release.CommandText = """
                UPDATE batch_prize_state
                SET reserved_count = reserved_count - 1,
                    available_count = available_count + 1
                WHERE batch_id = $batchId
                  AND tier_id = $tierId
                  AND reserved_count > 0;
                """;
            release.Parameters.AddWithValue("$batchId", pending.BatchId);
            release.Parameters.AddWithValue("$tierId", pending.ReservedTierId);
            if (await release.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Pending Ticket 的保留獎項狀態不一致。");

            var consumeLoss = connection.CreateCommand();
            consumeLoss.Transaction = transaction;
            consumeLoss.CommandText = """
                UPDATE batch_prize_state
                SET available_count = available_count - 1,
                    consumed_count = consumed_count + 1
                WHERE batch_id = $batchId
                  AND tier_id = $tierId
                  AND amount = 0
                  AND available_count > 0;
                """;
            consumeLoss.Parameters.AddWithValue("$batchId", pending.BatchId);
            consumeLoss.Parameters.AddWithValue("$tierId", losingTierId);
            if (await consumeLoss.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("未中獎票已被其他操作更新，請重試。");
        }

        await InsertHistoryAsync(connection, transaction, pending, 0, cancellationToken);
        await DeletePendingAsync(connection, transaction, pending.Id, cancellationToken);
        transaction.Commit();
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

    private static async Task<string?> GetAvailableLosingTierIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string batchId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT tier_id
            FROM batch_prize_state
            WHERE batch_id = $batchId AND amount = 0 AND available_count > 0
            ORDER BY available_count DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$batchId", batchId);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task ConsumeReservedAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string batchId,
        string tierId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE batch_prize_state
            SET reserved_count = reserved_count - 1,
                consumed_count = consumed_count + 1
            WHERE batch_id = $batchId AND tier_id = $tierId AND reserved_count > 0;
            """;
        command.Parameters.AddWithValue("$batchId", batchId);
        command.Parameters.AddWithValue("$tierId", tierId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Pending Ticket 的保留狀態不一致。");
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
