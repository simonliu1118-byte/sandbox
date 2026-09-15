using ScratchGame.Data;
using ScratchGame.Models;

namespace ScratchGame.Services;

public sealed class UserProfileService(AppDatabase database)
{
    public async Task<UserProfile> RenameAsync(
        string userId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        displayName = displayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("使用者名稱不可空白。", nameof(displayName));
        if (displayName.Length > 30)
            throw new ArgumentException("使用者名稱不可超過 30 個字元。", nameof(displayName));

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var update = connection.CreateCommand();
        update.CommandText = "UPDATE users SET display_name = $name WHERE id = $id;";
        update.Parameters.AddWithValue("$name", displayName);
        update.Parameters.AddWithValue("$id", userId);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("找不到要修改的使用者。");

        return await ReadProfileAsync(connection, userId, cancellationToken);
    }

    public async Task<UserProfile> GrantWalletFundsAsync(
        string userId,
        long amount = AppDatabase.DefaultWalletGrantAmount,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var update = connection.CreateCommand();
        update.CommandText = """
            UPDATE users
            SET wallet_balance = wallet_balance + $amount,
                grant_count = grant_count + 1,
                grant_total_amount = grant_total_amount + $amount
            WHERE id = $id;
            """;
        update.Parameters.AddWithValue("$amount", amount);
        update.Parameters.AddWithValue("$id", userId);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("找不到要增加錢包資金的使用者。");

        return await ReadProfileAsync(connection, userId, cancellationToken);
    }

    // V0.4 起遊玩統計不可任意歸零；舊 UI 會在下一階段移除這個入口。
    public Task<UserProfile> ResetStatsAsync(
        string userId,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("V0.4 起不再提供重置遊玩統計功能。");

    private static async Task<UserProfile> ReadProfileAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string userId,
        CancellationToken cancellationToken)
    {
        var query = connection.CreateCommand();
        query.CommandText = """
            SELECT display_name,
                   total_spent, total_redeemed,
                   wallet_balance, completed_ticket_count, win_count, max_prize,
                   grant_count, grant_total_amount
            FROM users
            WHERE id = $id
            LIMIT 1;
            """;
        query.Parameters.AddWithValue("$id", userId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("找不到要修改的使用者。");

        return new UserProfile(
            userId,
            reader.GetString(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.GetInt64(7),
            reader.GetInt64(8));
    }
}
