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

        // 彩券定義不再由 SeedDataService 硬編碼建立。
        // Built-in Base Pack 與 Imported Pack 都必須走相同 ScratchPack V1 installer。
        transaction.Commit();

        // Built-in Base Pack 隨程式發行並在啟動時自動註冊；實際安裝仍走同一套 V1 importer。
        var builtInPacks = new BuiltInPackBootstrapService(database);
        await builtInPacks.EnsureInstalledAsync(cancellationToken);
    }
}
