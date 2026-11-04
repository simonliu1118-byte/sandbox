namespace ScratchGame.Models;

public enum ScratchPackInstallSource
{
    Imported,
    BuiltIn
}

public sealed record ScratchPackResourceRef(string Source, string Ref);

public sealed record ScratchPackRect(
    int X,
    int Y,
    int Width,
    int Height);

public sealed record ScratchPackZone(
    string Id,
    int X,
    int Y,
    int Width,
    int Height,
    string Shape,
    int? CornerRadius = null);

public sealed record ScratchPackPrize(long Amount, long Count);

public sealed record ScratchPackSymbolPrize(string Symbol, long Amount);

public sealed record ScratchPackManifestDefinition(
    string FormatVersion,
    Guid PackageId,
    string Author,
    Version MinimumAppVersion,
    string TicketFile);

public sealed record ScratchPackTicketDefinition(
    string Name,
    long Price,
    int Canvas,
    bool PriceDisplay,
    ScratchPackRect? PriceDisplayArea,
    string GameType,
    long IssueSize,
    long TicketsPerBook,
    ScratchPackResourceRef TicketArt,
    ScratchPackRect SerialDisplayArea,
    ScratchPackResourceRef Foil,
    IReadOnlyList<ScratchPackZone> Zones,
    int GridSize,
    bool AllowNearMiss,
    IReadOnlyList<ScratchPackPrize> Prizes,
    string RawJson,
    int? WinningNumberCount = null,
    int? PlayNumberCount = null,
    int? NumberMin = null,
    int? NumberMax = null,
    string? PayoutSource = null,
    IReadOnlyList<long>? DisplayPrizeAmounts = null,
    bool? AllowPrizeAmountRepeat = null,
    int? ZoneCount = null,
    bool? UseCustomDecoyAmounts = null,
    IReadOnlyList<long>? DecoyAmounts = null,
    int? NearMissPairProbability = null,
    int? NearMissPairCount = null,
    string? SymbolCountMode = null,
    string? TargetSymbol = null,
    int? MinimumMatchCount = null,
    int? MatchCount = null,
    IReadOnlyList<ScratchPackSymbolPrize>? SymbolPrizes = null,
    bool? AllowMultipleWins = null,
    bool? UseCustomDecoySymbols = null,
    IReadOnlyList<string>? DecoySymbols = null);

public sealed record LoadedScratchPack(
    ScratchPackManifestDefinition Manifest,
    ScratchPackTicketDefinition Ticket,
    string ManifestJson,
    string ContentHash);
