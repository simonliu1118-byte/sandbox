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

        const string ticketId = "builtin-three-line-100";
        var exists = connection.CreateCommand();
        exists.Transaction = transaction;
        exists.CommandText = "SELECT 1 FROM ticket_definitions WHERE id = $id LIMIT 1;";
        exists.Parameters.AddWithValue("$id", ticketId);

        if (await exists.ExecuteScalarAsync(cancellationToken) is null)
        {
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

            var addTicket = connection.CreateCommand();
            addTicket.Transaction = transaction;
            addTicket.CommandText = """
                INSERT INTO ticket_definitions(
                    id, display_name, price, rule_id, issue_size,
                    published_win_rate, enabled, locked, source_package_id, created_utc)
                VALUES($id, '三星連線', 100, 'ThreeLine', $issueSize,
                       $winRate, 1, 1, NULL, $createdUtc);
                """;
            addTicket.Parameters.AddWithValue("$id", ticketId);
            addTicket.Parameters.AddWithValue("$issueSize", issueSize);
            addTicket.Parameters.AddWithValue("$winRate", winRate);
            addTicket.Parameters.AddWithValue("$createdUtc", DateTimeOffset.UtcNow.ToString("O"));
            await addTicket.ExecuteNonQueryAsync(cancellationToken);

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

        transaction.Commit();
    }
}
