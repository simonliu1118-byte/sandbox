using ScratchGame.Data;
using ScratchGame.Models;

namespace ScratchGame.Services;

public sealed record ResolvedScratchPackTicket(
    ScratchPackTicketDefinition Definition,
    string PackageRoot,
    string TicketImagePath,
    string FoilImagePath);

public sealed class ScratchPackRuntimeService(AppDatabase database)
{
    private readonly ScratchPackV1Loader _loader = new();

    public ResolvedScratchPackTicket Load(TicketDefinition runtimeTicket)
    {
        if (string.IsNullOrWhiteSpace(runtimeTicket.SourcePackageId))
            throw new InvalidOperationException("此彩券不是 ScratchPack V1 安裝項目。");

        var packageRoot = Path.Combine(database.DataDirectory, "packages", runtimeTicket.SourcePackageId);
        var definition = _loader.LoadInstalledTicket(packageRoot);

        var ticketImagePath = ResolveResourcePath(
            definition.TicketArt,
            packageRoot,
            definition.GameType,
            isTicket: true);
        var foilImagePath = ResolveResourcePath(
            definition.Foil,
            packageRoot,
            definition.GameType,
            isTicket: false);

        if (!File.Exists(ticketImagePath))
            throw new InvalidDataException($"找不到票面資源：{ticketImagePath}");
        if (!File.Exists(foilImagePath))
            throw new InvalidDataException($"找不到銀膜資源：{foilImagePath}");

        return new ResolvedScratchPackTicket(
            definition,
            packageRoot,
            ticketImagePath,
            foilImagePath);
    }

    private static string ResolveResourcePath(
        ScratchPackResourceRef resource,
        string packageRoot,
        string gameType,
        bool isTicket)
    {
        if (resource.Source == "builtin")
        {
            return isTicket
                ? ScratchPackV1Loader.ResolveBuiltInTicketPath(gameType, resource.Ref)
                : ScratchPackV1Loader.ResolveBuiltInFoilPath(resource.Ref);
        }

        return ScratchPackV1Loader.ResolveInside(packageRoot, resource.Ref);
    }
}
