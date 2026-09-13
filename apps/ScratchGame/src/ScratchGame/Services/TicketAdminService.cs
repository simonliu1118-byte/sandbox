using ScratchGame.Data;
using ScratchGame.Engine;

namespace ScratchGame.Services;

public sealed record TicketAdminItem(
    string Id,
    string DisplayName,
    long Price,
    string RuleId,
    string RuleDisplayName,
    long IssueSize,
    long TicketsPerBook,
    long StyleNumber,
    int PriceDisplay,
    double PublishedWinRate,
    bool Enabled,
    bool Locked,
    int? ActiveBatchNumber,
    string? SourcePackageId)
{
    public long BookCount => IssueSize / TicketsPerBook;
    public string StatusText => Enabled ? "啟用中" : "已停用";
    public string BatchText => ActiveBatchNumber is int number ? $"第 {number} 批" : "未發行";
}

public sealed record TicketPrizePoolRow(
    long Amount,
    long InitialCount,
    long AvailableCount,
    long ReservedCount,
    long ConsumedCount);

public sealed record TicketAdminDetail(
    long IssueSize,
    long TicketsPerBook,
    long BookCount,
    int? ActiveBatchNumber,
    long AvailableCount,
    long ReservedCount,
    long ConsumedCount,
    IReadOnlyList<TicketPrizePoolRow> PrizeRows);

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
                   COALESCE(m.tickets_per_book, t.issue_size),
                   COALESCE(m.style_number, 0),
                   COALESCE(m.price_display, 0),
                   t.published_win_rate, t.enabled, t.locked, t.source_package_id,
                   (SELECT b.batch_number
                    FROM batches b
                    WHERE b.ticket_id = t.id AND b.status = 'Active'
                    LIMIT 1) AS active_batch
            FROM ticket_definitions t
            LEFT JOIN ticket_metadata m ON m.ticket_id = t.id
            ORDER BY t.enabled DESC, t.price, t.display_name;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var ruleId = reader.GetString(3);
            var issueSize = reader.GetInt64(4);
            var ticketsPerBook = reader.GetInt64(5);
            if (ticketsPerBook <= 0 || issueSize % ticketsPerBook != 0)
                ticketsPerBook = issueSize;

            result.Add(new TicketAdminItem(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                ruleId,
                GameTypeCatalog.GetDisplayName(ruleId),
                issueSize,
                ticketsPerBook,
                reader.GetInt64(6),
                reader.GetInt32(7),
                reader.GetDouble(8),
                reader.GetInt64(9) != 0,
                reader.GetInt64(10) != 0,
                reader.IsDBNull(12) ? null : reader.GetInt32(12),
                reader.IsDBNull(11) ? null : reader.GetString(11)));
        }
        return result;
    }

    public async Task<TicketAdminDetail> GetTicketDetailAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);

        long issueSize;
        long ticketsPerBook;
        string? batchId = null;
        int? batchNumber = null;

        var header = connection.CreateCommand();
        header.CommandText = """
            SELECT t.issue_size,
                   COALESCE(m.tickets_per_book, t.issue_size),
                   b.id, b.batch_number
            FROM ticket_definitions t
            LEFT JOIN ticket_metadata m ON m.ticket_id = t.id
            LEFT JOIN batches b ON b.ticket_id = t.id AND b.status = 'Active'
            WHERE t.id = $ticketId
            LIMIT 1;
            """;
        header.Parameters.AddWithValue("$ticketId", ticketId);
        await using (var reader = await header.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("找不到指定彩券。");
            issueSize = reader.GetInt64(0);
            ticketsPerBook = reader.GetInt64(1);
            if (!reader.IsDBNull(2)) batchId = reader.GetString(2);
            if (!reader.IsDBNull(3)) batchNumber = reader.GetInt32(3);
        }

        if (ticketsPerBook <= 0 || issueSize % ticketsPerBook != 0)
            throw new InvalidOperationException("此彩券的每本張數設定不合法。");

        var rows = new List<TicketPrizePoolRow>();
        if (batchId is null)
        {
            var initial = connection.CreateCommand();
            initial.CommandText = """
                SELECT amount, initial_count
                FROM prize_tiers
                WHERE ticket_id = $ticketId
                ORDER BY sort_order, amount DESC;
                """;
            initial.Parameters.AddWithValue("$ticketId", ticketId);
            await using var reader = await initial.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                rows.Add(new TicketPrizePoolRow(reader.GetInt64(0), reader.GetInt64(1), 0, 0, 0));
        }
        else
        {
            var pool = connection.CreateCommand();
            pool.CommandText = """
                SELECT p.amount, p.initial_count,
                       COALESCE(s.available_count, 0),
                       COALESCE(s.reserved_count, 0),
                       COALESCE(s.consumed_count, 0)
                FROM prize_tiers p
                LEFT JOIN batch_prize_state s
                  ON s.batch_id = $batchId AND s.tier_id = p.tier_id
                WHERE p.ticket_id = $ticketId
                ORDER BY p.sort_order, p.amount DESC;
                """;
            pool.Parameters.AddWithValue("$batchId", batchId);
            pool.Parameters.AddWithValue("$ticketId", ticketId);
            await using var reader = await pool.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new TicketPrizePoolRow(
                    reader.GetInt64(0), reader.GetInt64(1),
                    reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4)));
            }
        }

        return new TicketAdminDetail(
            issueSize,
            ticketsPerBook,
            issueSize / ticketsPerBook,
            batchNumber,
            rows.Sum(row => row.AvailableCount),
            rows.Sum(row => row.ReservedCount),
            rows.Sum(row => row.ConsumedCount),
            rows);
    }

    public Task<string> ImportScratchPackAsync(string path, CancellationToken cancellationToken = default)
        => _importer.ImportAsync(path, cancellationToken);

    public Task<int> StartNextBatchAsync(string ticketId, CancellationToken cancellationToken = default)
        => _catalog.StartNextBatchAsync(ticketId, cancellationToken);

    public async Task SetEnabledAsync(string ticketId, bool enabled, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE ticket_definitions SET enabled = $enabled WHERE id = $id;";
        command.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
        command.Parameters.AddWithValue("$id", ticketId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("找不到指定彩券。");
    }

    public async Task DeleteNeverIssuedAsync(string ticketId, CancellationToken cancellationToken = default)
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

            var deleteMetadata = connection.CreateCommand();
            deleteMetadata.Transaction = transaction;
            deleteMetadata.CommandText = "DELETE FROM ticket_metadata WHERE ticket_id = $id;";
            deleteMetadata.Parameters.AddWithValue("$id", ticketId);
            await deleteMetadata.ExecuteNonQueryAsync(cancellationToken);

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
