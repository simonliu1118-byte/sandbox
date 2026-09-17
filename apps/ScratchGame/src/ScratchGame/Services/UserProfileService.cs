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

    public async Task DeleteAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        var exists = connection.CreateCommand();
        exists.Transaction = transaction;
        exists.CommandText = "SELECT COUNT(*) FROM users WHERE id = $id;";
        exists.Parameters.AddWithValue("$id", userId);
        if (Convert.ToInt64(await exists.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new InvalidOperationException("找不到要刪除的玩家。");

        var count = connection.CreateCommand();
        count.Transaction = transaction;
        count.CommandText = "SELECT COUNT(*) FROM users;";
        if (Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken)) <= 1)
            throw new InvalidOperationException("至少必須保留一個玩家。");

        var pending = connection.CreateCommand();
        pending.Transaction = transaction;
        pending.CommandText = "SELECT 1 FROM pending_tickets WHERE user_id = $id LIMIT 1;";
        pending.Parameters.AddWithValue("$id", userId);
        if (await pending.ExecuteScalarAsync(cancellationToken) is not null)
            throw new InvalidOperationException("這位玩家還有未完成的彩券，請先完成或處理該張彩券後再刪除。");

        var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM users WHERE id = $id;";
        delete.Parameters.AddWithValue("$id", userId);
        if (await delete.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("刪除玩家失敗，請再試一次。");

        await transaction.CommitAsync(cancellationToken);
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
