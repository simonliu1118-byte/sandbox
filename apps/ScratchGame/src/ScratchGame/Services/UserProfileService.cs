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

        var query = connection.CreateCommand();
        query.CommandText = "SELECT display_name, total_spent, total_redeemed FROM users WHERE id = $id LIMIT 1;";
        query.Parameters.AddWithValue("$id", userId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("找不到要修改的使用者。");

        return new UserProfile(userId, reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    public async Task<UserProfile> ResetStatsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var update = connection.CreateCommand();
        update.CommandText = "UPDATE users SET total_spent = 0, total_redeemed = 0 WHERE id = $id;";
        update.Parameters.AddWithValue("$id", userId);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("找不到要重置的使用者。");

        var query = connection.CreateCommand();
        query.CommandText = "SELECT display_name FROM users WHERE id = $id LIMIT 1;";
        query.Parameters.AddWithValue("$id", userId);
        var displayName = Convert.ToString(await query.ExecuteScalarAsync(cancellationToken));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvalidOperationException("找不到要重置的使用者。");

        return new UserProfile(userId, displayName, 0, 0);
    }
}
