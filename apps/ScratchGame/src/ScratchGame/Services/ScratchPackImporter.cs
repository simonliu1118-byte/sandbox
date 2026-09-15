using ScratchGame.Data;
using ScratchGame.Models;

namespace ScratchGame.Services;

public sealed class ScratchPackImporter(AppDatabase database)
{
    private readonly ScratchPackV1Loader _loader = new();

    public Task<string> ImportAsync(
        string scratchPackPath,
        CancellationToken cancellationToken = default)
        => InstallAsync(scratchPackPath, ScratchPackInstallSource.Imported, cancellationToken);

    public async Task<string> InstallAsync(
        string scratchPackPath,
        ScratchPackInstallSource installSource,
        CancellationToken cancellationToken = default)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ScratchGame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        string? finalPackageDirectory = null;
        try
        {
            var loaded = _loader.LoadAndValidate(scratchPackPath, tempRoot);
            var packageId = loaded.Manifest.PackageId.ToString("D");
            var ticketId = "pack-" + loaded.Manifest.PackageId.ToString("N");

            await using var connection = await database.OpenConnectionAsync(cancellationToken);

            var installed = connection.CreateCommand();
            installed.CommandText = """
                SELECT ticket_id, source_kind, content_hash
                FROM scratchpack_installations
                WHERE package_id = $packageId
                LIMIT 1;
                """;
            installed.Parameters.AddWithValue("$packageId", packageId);
            await using (var reader = await installed.ExecuteReaderAsync(cancellationToken))
            {
                if (await reader.ReadAsync(cancellationToken))
                {
                    var existingTicketId = reader.GetString(0);
                    var existingSource = reader.GetString(1);
                    var existingHash = reader.GetString(2);
                    if (installSource == ScratchPackInstallSource.BuiltIn &&
                        existingSource == "BuiltIn" &&
                        string.Equals(existingHash, loaded.ContentHash, StringComparison.OrdinalIgnoreCase))
                    {
                        return existingTicketId;
                    }

                    if (installSource == ScratchPackInstallSource.BuiltIn && existingSource == "BuiltIn")
                        throw new InvalidOperationException("Built-in ScratchPack 的 packageId 未改變，但封裝內容已變更。請建立新的 packageId，避免偷偷改寫已發布 Pack。");
                    throw new InvalidOperationException("相同 packageId 的 ScratchPack 已安裝。");
                }
            }

            var collision = connection.CreateCommand();
            collision.CommandText = "SELECT COUNT(*) FROM ticket_definitions WHERE id = $ticketId OR source_package_id = $packageId;";
            collision.Parameters.AddWithValue("$ticketId", ticketId);
            collision.Parameters.AddWithValue("$packageId", packageId);
            if (Convert.ToInt64(await collision.ExecuteScalarAsync(cancellationToken)) > 0)
                throw new InvalidOperationException("相同 runtime ticket id 或 packageId 的彩券已存在。");

            var packageDirectory = Path.Combine(database.DataDirectory, "packages");
            Directory.CreateDirectory(packageDirectory);
            finalPackageDirectory = Path.Combine(packageDirectory, packageId);
            if (Directory.Exists(finalPackageDirectory))
                throw new InvalidOperationException("packageId 的安裝資料夾已存在，但資料庫沒有對應安裝紀錄；請先清理不一致資料。");

            Directory.Move(tempRoot, finalPackageDirectory);
            tempRoot = string.Empty;

            using var transaction = connection.BeginTransaction();
            try
            {
                var winningCount = loaded.Ticket.Prizes.Sum(p => p.Count);
                var loseCount = loaded.Ticket.IssueSize - winningCount;
                var winRate = (double)winningCount / loaded.Ticket.IssueSize;

                var addTicket = connection.CreateCommand();
                addTicket.Transaction = transaction;
                addTicket.CommandText = """
                    INSERT INTO ticket_definitions(
                        id, display_name, price, rule_id, issue_size,
                        published_win_rate, enabled, locked, source_package_id, created_utc)
                    VALUES($id, $name, $price, $gameType, $issueSize,
                           $winRate, 1, 0, $packageId, $createdUtc);
                    """;
                addTicket.Parameters.AddWithValue("$id", ticketId);
                addTicket.Parameters.AddWithValue("$name", loaded.Ticket.Name);
                addTicket.Parameters.AddWithValue("$price", loaded.Ticket.Price);
                addTicket.Parameters.AddWithValue("$gameType", loaded.Ticket.GameType);
                addTicket.Parameters.AddWithValue("$issueSize", loaded.Ticket.IssueSize);
                addTicket.Parameters.AddWithValue("$winRate", winRate);
                addTicket.Parameters.AddWithValue("$packageId", packageId);
                addTicket.Parameters.AddWithValue("$createdUtc", DateTimeOffset.UtcNow.ToString("O"));
                await addTicket.ExecuteNonQueryAsync(cancellationToken);

                var order = 0;
                foreach (var prize in loaded.Ticket.Prizes.OrderBy(p => p.Amount))
                {
                    if (prize.Count > 0)
                    {
                        await InsertRuntimeTierAsync(
                            connection, transaction, ticketId,
                            $"win-{order + 1:00}", prize.Amount, prize.Count, order,
                            cancellationToken);
                    }
                    order++;
                }

                if (loseCount > 0)
                {
                    await InsertRuntimeTierAsync(
                        connection, transaction, ticketId,
                        "lose", 0, loseCount, int.MaxValue,
                        cancellationToken);
                }

                var addInstallation = connection.CreateCommand();
                addInstallation.Transaction = transaction;
                addInstallation.CommandText = """
                    INSERT INTO scratchpack_installations(
                        package_id, ticket_id, source_kind, content_hash, installed_utc)
                    VALUES($packageId, $ticketId, $sourceKind, $contentHash, $installedUtc);
                    """;
                addInstallation.Parameters.AddWithValue("$packageId", packageId);
                addInstallation.Parameters.AddWithValue("$ticketId", ticketId);
                addInstallation.Parameters.AddWithValue("$sourceKind", installSource == ScratchPackInstallSource.BuiltIn ? "BuiltIn" : "Imported");
                addInstallation.Parameters.AddWithValue("$contentHash", loaded.ContentHash);
                addInstallation.Parameters.AddWithValue("$installedUtc", DateTimeOffset.UtcNow.ToString("O"));
                await addInstallation.ExecuteNonQueryAsync(cancellationToken);

                transaction.Commit();
            }
            catch
            {
                try { transaction.Rollback(); } catch { }
                if (Directory.Exists(finalPackageDirectory))
                    Directory.Delete(finalPackageDirectory, recursive: true);
                throw;
            }

            return ticketId;
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempRoot) && Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static async Task InsertRuntimeTierAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        string ticketId,
        string tierId,
        long amount,
        long count,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO prize_tiers(ticket_id, tier_id, amount, initial_count, sort_order)
            VALUES($ticketId, $tierId, $amount, $count, $sortOrder);
            """;
        command.Parameters.AddWithValue("$ticketId", ticketId);
        command.Parameters.AddWithValue("$tierId", tierId);
        command.Parameters.AddWithValue("$amount", amount);
        command.Parameters.AddWithValue("$count", count);
        command.Parameters.AddWithValue("$sortOrder", sortOrder);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
