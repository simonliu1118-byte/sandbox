using ScratchGame.Data;
using ScratchGame.Models;

namespace ScratchGame.Services;

public sealed class BuiltInPackBootstrapService
{
    private readonly AppDatabase _database;
    private readonly ScratchPackImporter _importer;
    private readonly CatalogService _catalog;

    public BuiltInPackBootstrapService(AppDatabase database)
    {
        _database = database;
        _importer = new ScratchPackImporter(database);
        _catalog = new CatalogService(database);
    }

    public async Task EnsureInstalledAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "BuiltInPacks");
        if (!Directory.Exists(directory))
            return;

        foreach (var path in Directory.EnumerateFiles(directory, "*.scratchpack", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var ticketId = await _importer.InstallAsync(
                path,
                ScratchPackInstallSource.BuiltIn,
                cancellationToken);
            await EnsureInitialBatchAsync(ticketId, cancellationToken);
        }
    }

    private async Task EnsureInitialBatchAsync(
        string ticketId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM batches WHERE ticket_id = $ticketId;";
        command.Parameters.AddWithValue("$ticketId", ticketId);
        var batchCount = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        if (batchCount == 0)
            await _catalog.StartNextBatchAsync(ticketId, cancellationToken);
    }
}
