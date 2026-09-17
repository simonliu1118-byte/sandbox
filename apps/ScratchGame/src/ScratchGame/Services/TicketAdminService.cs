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
    string? SourcePackageId,
    string? SourceKind)
{
    public long BookCount => IssueSize / TicketsPerBook;
    public string BatchText => ActiveBatchNumber is int number ? $"第 {number} 批" : "未發行";
    public bool IsBuiltIn => string.Equals(SourceKind, "BuiltIn", StringComparison.Ordinal);
    public bool IsImported => string.Equals(SourceKind, "Imported", StringComparison.Ordinal);
    public string SourceText => IsBuiltIn ? "內建" : IsImported ? "匯入" : "舊資料";
    public bool CanUninstall => IsImported;
}

public sealed record TicketPrizePoolRow(
    long Amount,
    long InitialCount,
    long RemainingCount);

public sealed record TicketAdminDetail(
    long IssueSize,
    long TicketsPerBook,
    long BookCount,
    int? ActiveBatchNumber,
    long RemainingCount,
    IReadOnlyList<TicketPrizePoolRow> PrizeRows);

public sealed class TicketAdminService
{
    private readonly AppDatabase _database;
    private readonly CatalogService _catalog;
    private readonly ScratchPackImporter _importer;
    private readonly TicketThumbnailCacheService _thumbnailCache;

    public TicketAdminService(AppDatabase database)
    {
        _database = database;
        _catalog = new CatalogService(database);
        _importer = new ScratchPackImporter(database);
        _thumbnailCache = new TicketThumbnailCacheService(database);
    }

    public Task<IReadOnlyList<TicketAdminItem>> GetTicketsAsync(
        CancellationToken cancellationToken = default)
        => QueryTicketsAsync(hidden: false, packsOnly: false, cancellationToken);

    public Task<IReadOnlyList<TicketAdminItem>> GetHiddenPacksAsync(
        CancellationToken cancellationToken = default)
        => QueryTicketsAsync(hidden: true, packsOnly: true, cancellationToken);

    private async Task<IReadOnlyList<TicketAdminItem>> QueryTicketsAsync(
        bool hidden,
        bool packsOnly,
        CancellationToken cancellationToken)
    {
        var result = new List<TicketAdminItem>();
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT t.id, t.display_name, t.price, t.rule_id, t.issue_size,
                   COALESCE(m.tickets_per_book, t.issue_size),
                   COALESCE(m.style_number, 0),
                   COALESCE(m.price_display, 0),
                   t.published_win_rate, t.enabled, t.locked, t.source_package_id,
                   (SELECT b.batch_number
                    FROM batches b
                    WHERE b.ticket_id = t.id AND b.status = 'Active'
                    LIMIT 1) AS active_batch,
                   (SELECT s.source_kind
                    FROM scratchpack_installations s
                    WHERE s.ticket_id = t.id
                    LIMIT 1) AS source_kind
            FROM ticket_definitions t
            LEFT JOIN ticket_metadata m ON m.ticket_id = t.id
            WHERE t.enabled = {(hidden ? 0 : 1)}
              {(packsOnly ? "AND t.source_package_id IS NOT NULL AND EXISTS (SELECT 1 FROM scratchpack_installations s WHERE s.ticket_id = t.id)" : string.Empty)}
            ORDER BY t.price, t.display_name;
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
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(13) ? null : reader.GetString(13)));
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
                rows.Add(new TicketPrizePoolRow(reader.GetInt64(0), reader.GetInt64(1), 0));
        }
        else
        {
            var pool = connection.CreateCommand();
            pool.CommandText = """
                SELECT p.amount, p.initial_count,
                       COALESCE(s.available_count, 0)
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
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetInt64(2)));
            }
        }

        return new TicketAdminDetail(
            issueSize,
            ticketsPerBook,
            issueSize / ticketsPerBook,
            batchNumber,
            rows.Sum(row => row.RemainingCount),
            rows);
    }

    public async Task<string> ImportScratchPackAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var ticketId = await _importer.ImportAsync(path, cancellationToken);
        await _catalog.EnsureInitialBatchAsync(ticketId, cancellationToken);
        return ticketId;
    }

    public Task<int> StartNextBatchAsync(string ticketId, CancellationToken cancellationToken = default)
        => _catalog.StartNextBatchAsync(ticketId, cancellationToken);

    public async Task SetHiddenAsync(
        string ticketId,
        bool hidden,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE ticket_definitions SET enabled = $enabled WHERE id = $id;";
        command.Parameters.AddWithValue("$enabled", hidden ? 0 : 1);
        command.Parameters.AddWithValue("$id", ticketId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("找不到指定彩券。");
    }

    public async Task UninstallPackAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        string packageId;
        string sourceKind;

        await using (var connection = await _database.OpenConnectionAsync(cancellationToken))
        {
            var info = connection.CreateCommand();
            info.CommandText = """
                SELECT t.source_package_id,
                       s.source_kind,
                       (SELECT COUNT(*) FROM pending_tickets p WHERE p.ticket_id = t.id)
                FROM ticket_definitions t
                LEFT JOIN scratchpack_installations s ON s.ticket_id = t.id
                WHERE t.id = $id
                LIMIT 1;
                """;
            info.Parameters.AddWithValue("$id", ticketId);
            await using var reader = await info.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("找不到指定彩券。");
            if (reader.IsDBNull(0) || reader.IsDBNull(1))
                throw new InvalidOperationException("這不是目前已安裝的 ScratchPack。");

            packageId = reader.GetString(0);
            sourceKind = reader.GetString(1);
            var pendingCount = reader.GetInt64(2);

            if (sourceKind == "BuiltIn")
                throw new InvalidOperationException("Built-in Pack 不可解除安裝；不使用時請改用「隱藏」。");
            if (pendingCount > 0)
                throw new InvalidOperationException("此 Pack 仍有尚未完成的彩券，請先完成兌獎後再解除安裝。");
        }

        var packageDirectory = Path.Combine(_database.DataDirectory, "packages", packageId);
        var movedDirectory = packageDirectory + ".uninstall-" + Guid.NewGuid().ToString("N");
        var packageMoved = false;
        if (Directory.Exists(packageDirectory))
        {
            Directory.Move(packageDirectory, movedDirectory);
            packageMoved = true;
        }

        try
        {
            await using var connection = await _database.OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();

            var deleteBatches = connection.CreateCommand();
            deleteBatches.Transaction = transaction;
            deleteBatches.CommandText = "DELETE FROM batches WHERE ticket_id = $id;";
            deleteBatches.Parameters.AddWithValue("$id", ticketId);
            await deleteBatches.ExecuteNonQueryAsync(cancellationToken);

            var deleteInstallation = connection.CreateCommand();
            deleteInstallation.Transaction = transaction;
            deleteInstallation.CommandText = "DELETE FROM scratchpack_installations WHERE ticket_id = $id;";
            deleteInstallation.Parameters.AddWithValue("$id", ticketId);
            await deleteInstallation.ExecuteNonQueryAsync(cancellationToken);

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
            if (await deleteTicket.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("解除安裝時找不到指定彩券。");

            transaction.Commit();
        }
        catch
        {
            if (packageMoved && Directory.Exists(movedDirectory) && !Directory.Exists(packageDirectory))
                Directory.Move(movedDirectory, packageDirectory);
            throw;
        }

        if (packageMoved && Directory.Exists(movedDirectory))
            Directory.Delete(movedDirectory, recursive: true);
        _thumbnailCache.DeleteForPackage(packageId);
    }
}
