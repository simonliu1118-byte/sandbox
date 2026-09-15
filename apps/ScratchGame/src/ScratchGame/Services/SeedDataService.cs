using ScratchGame.Data;

namespace ScratchGame.Services;

public sealed class SeedDataService(AppDatabase database)
{
    private static readonly string[] RetiredLegacyTicketIds =
    [
        "builtin-three-line-100",
        "builtin-star-line-500-v1",
        "builtin-star-line-500-prize-test"
    ];

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

        // V0.3 and earlier seeded three development tickets directly into the database.
        // Existing %LOCALAPPDATA% databases still contain those rows after replacing the EXE.
        // Retire them non-destructively so pending/history references remain valid while they
        // disappear from the normal catalog and V0.4 settings UI.
        foreach (var ticketId in RetiredLegacyTicketIds)
        {
            var retire = connection.CreateCommand();
            retire.Transaction = transaction;
            retire.CommandText = "UPDATE ticket_definitions SET enabled = 0 WHERE id = $id;";
            retire.Parameters.AddWithValue("$id", ticketId);
            await retire.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }
}
