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
                INSERT INTO users(
                    id, display_name,
                    total_spent, total_redeemed,
                    wallet_balance, completed_ticket_count, win_count, max_prize,
                    grant_count, grant_total_amount,
                    created_utc)
                VALUES(
                    $id, '玩家 1',
                    0, 0,
                    $wallet, 0, 0, 0,
                    0, 0,
                    $createdUtc);
                """;
            addUser.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
            addUser.Parameters.AddWithValue("$wallet", AppDatabase.InitialWalletBalance);
            addUser.Parameters.AddWithValue("$createdUtc", DateTimeOffset.UtcNow.ToString("O"));
            await addUser.ExecuteNonQueryAsync(cancellationToken);
        }

        // V0.4 不再攜帶或辨識舊開發期 seed 彩券。測試舊資料由使用者自行清除，
        // 正式彩券一律由 ScratchPack lifecycle 管理。
        transaction.Commit();
    }
}
