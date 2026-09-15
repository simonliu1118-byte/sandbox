using ScratchGame.Data;
using ScratchGame.Models;

namespace ScratchGame.Services;

public sealed class BuiltInPackBootstrapService
{
    private readonly ScratchPackImporter _importer;
    private readonly CatalogService _catalog;

    public BuiltInPackBootstrapService(AppDatabase database)
    {
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
            await _catalog.EnsureInitialBatchAsync(ticketId, cancellationToken);
        }
    }
}
