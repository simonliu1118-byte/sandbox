using System.Text.Json;
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
    public ResolvedScratchPackTicket Load(TicketDefinition runtimeTicket)
    {
        if (string.IsNullOrWhiteSpace(runtimeTicket.SourcePackageId))
            throw new InvalidOperationException("此彩券不是 ScratchPack V1 安裝項目。");

        var packageRoot = Path.Combine(database.DataDirectory, "packages", runtimeTicket.SourcePackageId);
        var ticketPath = Path.Combine(packageRoot, "ticket.json");
        if (!File.Exists(ticketPath))
            throw new InvalidDataException("已安裝 ScratchPack 缺少 ticket.json。");

        var json = File.ReadAllText(ticketPath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var name = root.GetProperty("name").GetString() ?? string.Empty;
        var price = root.GetProperty("price").GetInt64();
        var canvas = root.GetProperty("canvas").GetInt32();
        var priceDisplay = root.GetProperty("priceDisplay").GetInt32() == 1;
        ScratchPackRect? priceArea = null;
        if (priceDisplay)
            priceArea = ParseRect(root.GetProperty("priceDisplayArea"));

        var gameType = root.GetProperty("gameType").GetString() ?? string.Empty;
        var issueSize = root.GetProperty("issueSize").GetInt64();
        var ticketsPerBook = root.GetProperty("ticketsPerBook").GetInt64();

        var art = root.GetProperty("art");
        var ticketArt = ParseResourceRef(art.GetProperty("ticket"));
        var serialArea = ParseRect(root.GetProperty("serialDisplayArea"));

        var scratch = root.GetProperty("scratch");
        var foil = ParseResourceRef(scratch.GetProperty("foil"));
        var zones = scratch.GetProperty("zones").EnumerateArray()
            .Select(ParseZone)
            .ToArray();

        var game = root.GetProperty("game");
        var gridSize = game.GetProperty("gridSize").GetInt32();
        var allowNearMiss = game.TryGetProperty("allowNearMiss", out var nearMiss) && nearMiss.GetBoolean();
        var prizes = root.GetProperty("prizes").EnumerateArray()
            .Select(p => new ScratchPackPrize(
                p.GetProperty("amount").GetInt64(),
                p.GetProperty("count").GetInt64()))
            .OrderBy(p => p.Amount)
            .ToArray();

        var definition = new ScratchPackTicketDefinition(
            name,
            price,
            canvas,
            priceDisplay,
            priceArea,
            gameType,
            issueSize,
            ticketsPerBook,
            ticketArt,
            serialArea,
            foil,
            zones,
            gridSize,
            allowNearMiss,
            prizes,
            json);

        var ticketImagePath = ResolveResourcePath(ticketArt, packageRoot, gameType, isTicket: true);
        var foilImagePath = ResolveResourcePath(foil, packageRoot, gameType, isTicket: false);
        if (!File.Exists(ticketImagePath))
            throw new InvalidDataException($"找不到票面資源：{ticketImagePath}");
        if (!File.Exists(foilImagePath))
            throw new InvalidDataException($"找不到銀膜資源：{foilImagePath}");

        return new ResolvedScratchPackTicket(definition, packageRoot, ticketImagePath, foilImagePath);
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

    private static ScratchPackResourceRef ParseResourceRef(JsonElement element)
        => new(
            element.GetProperty("source").GetString() ?? string.Empty,
            element.GetProperty("ref").GetString() ?? string.Empty);

    private static ScratchPackRect ParseRect(JsonElement element)
        => new(
            element.GetProperty("x").GetInt32(),
            element.GetProperty("y").GetInt32(),
            element.GetProperty("width").GetInt32(),
            element.GetProperty("height").GetInt32());

    private static ScratchPackZone ParseZone(JsonElement element)
    {
        int? cornerRadius = null;
        if (element.TryGetProperty("cornerRadius", out var radius))
            cornerRadius = radius.GetInt32();
        return new ScratchPackZone(
            element.GetProperty("id").GetString() ?? string.Empty,
            element.GetProperty("x").GetInt32(),
            element.GetProperty("y").GetInt32(),
            element.GetProperty("width").GetInt32(),
            element.GetProperty("height").GetInt32(),
            element.GetProperty("shape").GetString() ?? string.Empty,
            cornerRadius);
    }
}
