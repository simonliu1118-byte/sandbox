using Microsoft.Data.Sqlite;
using ScratchGame.Data;
using ScratchGame.Models;

namespace ScratchGame.Services;

public sealed class TicketSwapService(AppDatabase database)
{
    public async Task<PendingTicket> SwapPendingAsync(
        string pendingTicketId,
        Func<long, string> payloadFactory,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var current = await GetPendingAsync(connection, transaction, pendingTicketId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(current.ScratchStateJson))
            throw new InvalidOperationException("這張彩券已經開始刮獎，不能再換票。");

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
        release.Parameters.AddWithValue("$batchId", current.BatchId);
        release.Parameters.AddWithValue("$tierId", current.ReservedTierId);
        if (await release.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("原彩券的保留獎項狀態不一致，不能換票。");

        var tiers = await GetAvailableTiersAsync(connection, transaction, current.BatchId, cancellationToken);
        if (tiers.Count == 0)
            throw new InvalidOperationException("目前批次已沒有可換的彩券。");

        var selected = DrawWeighted(tiers);
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
        reserve.Parameters.AddWithValue("$batchId", current.BatchId);
        reserve.Parameters.AddWithValue("$tierId", selected.TierId);
        if (await reserve.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("獎池已被更新，請重新換票。");

        var newId = Guid.NewGuid().ToString("D");
        var createdAt = DateTimeOffset.UtcNow;
        var payload = payloadFactory(selected.Amount);

        var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM pending_tickets WHERE id = $id;";
        delete.Parameters.AddWithValue("$id", current.Id);
        if (await delete.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("原彩券狀態已改變，不能換票。");

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
        insert.Parameters.AddWithValue("$id", newId);
        insert.Parameters.AddWithValue("$userId", current.UserId);
        insert.Parameters.AddWithValue("$ticketId", current.TicketId);
        insert.Parameters.AddWithValue("$batchId", current.BatchId);
        insert.Parameters.AddWithValue("$tierId", selected.TierId);
        insert.Parameters.AddWithValue("$amount", selected.Amount);
        insert.Parameters.AddWithValue("$price", current.Price);
        insert.Parameters.AddWithValue("$payload", payload);
        insert.Parameters.AddWithValue("$createdUtc", createdAt.ToString("O"));
        await insert.ExecuteNonQueryAsync(cancellationToken);

        transaction.Commit();
        return new PendingTicket(
            newId,
            current.UserId,
            current.TicketId,
            current.BatchId,
            selected.TierId,
            selected.Amount,
            current.Price,
            payload,
            createdAt);
    }

    public async Task MarkScratchStartedAsync(
        string pendingTicketId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE pending_tickets
            SET scratch_state_json = COALESCE(scratch_state_json, '{"started":true}')
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", pendingTicketId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<PendingSwapState> GetPendingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string pendingTicketId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id, user_id, ticket_id, batch_id,
                   reserved_tier_id, reserved_amount, price,
                   scratch_state_json
            FROM pending_tickets
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", pendingTicketId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("找不到目前彩券。");

        return new PendingSwapState(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.IsDBNull(7) ? null : reader.GetString(7));
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

    private sealed record PendingSwapState(
        string Id,
        string UserId,
        string TicketId,
        string BatchId,
        string ReservedTierId,
        long ReservedAmount,
        long Price,
        string? ScratchStateJson);

    private sealed record AvailableTier(string TierId, long Amount, long AvailableCount);
}
