using ScratchGame.Data;

namespace ScratchGame.Services;

public sealed record TicketAdminItem(
    string Id,
    string DisplayName,
    long Price,
    string RuleId,
    long IssueSize,
    double PublishedWinRate,
    bool Enabled,
    bool Locked,
    int? ActiveBatchNumber,
    string? SourcePackageId);

public sealed class TicketAdminService
{
    private readonly AppDatabase _database;
    private readonly CatalogService _catalog;
    private readonly ScratchPackImporter _importer;

    public TicketAdminService(AppDatabase database)
    {
        _database = database;
        _catalog = new CatalogService(database);
        _importer = new ScratchPackImporter(database);
    }

    public async Task<IReadOnlyList<TicketAdminItem>> GetTicketsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<TicketAdminItem>();
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.id, t.display_name, t.price, t.rule_id, t.issue_size,
                   t.published_win_rate, t.enabled, t.locked, t.source_package_id,
                   (SELECT b.batch_number
                    FROM batches b
                    WHERE b.ticket_id = t.id AND b.status = 'Active'
                    LIMIT 1) AS active_batch
            FROM ticket_definitions t
            ORDER BY t.price, t.display_name;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new TicketAdminItem(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetString(3),
                reader.GetInt64(4),
                reader.GetDouble(5),
                reader.GetInt64(6) != 0,
                reader.GetInt64(7) != 0,
                reader.IsDBNull(9) ? null : reader.GetInt32(9),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }
        return result;
    }

    public Task<string> ImportScratchPackAsync(
        string path,
        CancellationToken cancellationToken = default)
        => _importer.ImportAsync(path, cancellationToken);

    public Task<int> StartNextBatchAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
        => _catalog.StartNextBatchAsync(ticketId, cancellationToken);

    public async Task SetEnabledAsync(
        string ticketId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE ticket_definitions SET enabled = $enabled WHERE id = $id;";
        command.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
        command.Parameters.AddWithValue("$id", ticketId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("找不到指定彩券。");
    }

    public async Task DeleteNeverIssuedAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        string? packageId;
        await using (var connection = await _database.OpenConnectionAsync(cancellationToken))
        {
            using var transaction = connection.BeginTransaction();

            var info = connection.CreateCommand();
            info.Transaction = transaction;
            info.CommandText = """
                SELECT source_package_id,
                       (SELECT COUNT(*) FROM batches WHERE ticket_id = $id)
                FROM ticket_definitions
                WHERE id = $id;
                """;
            info.Parameters.AddWithValue("$id", ticketId);
            await using var reader = await info.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("找不到指定彩券。");
            packageId = reader.IsDBNull(0) ? null : reader.GetString(0);
            var batchCount = reader.GetInt64(1);
            await reader.DisposeAsync();

            if (batchCount > 0)
                throw new InvalidOperationException("這張彩券已經發行過，只能停用，不能刪除。");

            var deleteTiers = connection.CreateCommand();
            deleteTiers.Transaction = transaction;
            deleteTiers.CommandText = "DELETE FROM prize_tiers WHERE ticket_id = $id;";
            deleteTiers.Parameters.AddWithValue("$id", ticketId);
            await deleteTiers.ExecuteNonQueryAsync(cancellationToken);

            var deleteTicket = connection.CreateCommand();
            deleteTicket.Transaction = transaction;
            deleteTicket.CommandText = "DELETE FROM ticket_definitions WHERE id = $id;";
            deleteTicket.Parameters.AddWithValue("$id", ticketId);
            await deleteTicket.ExecuteNonQueryAsync(cancellationToken);

            transaction.Commit();
        }

        if (packageId is not null)
        {
            var packageDirectory = Path.Combine(_database.DataDirectory, "packages", packageId);
            if (Directory.Exists(packageDirectory))
                Directory.Delete(packageDirectory, recursive: true);
        }
    }
}
