using System.Text.Json;
using ScratchGame.Models;

namespace ScratchGame.Engine;

public sealed record GameType4RenderCell(
    int ZoneIndex,
    ScratchPackZone Zone,
    string Symbol);

public static class GameType4RenderModel
{
    public static IReadOnlyList<GameType4RenderCell> Build(
        ScratchPackTicketDefinition ticket,
        JsonElement payload)
    {
        if (ticket.GameType != "4")
            throw new InvalidDataException("GameType4RenderModel 只能處理 GameType 4。");

        if (payload.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("GameType 4 payload 必須是 object。");
        if (payload.GetProperty("gameType").GetString() != "4")
            throw new InvalidDataException("GameType 4 payload.gameType 無效。");
        if (payload.GetProperty("mode").GetString() != ticket.SymbolCountMode)
            throw new InvalidDataException("GameType 4 payload.mode 與 ScratchPack 定義不一致。");

        var prizeAmount = payload.GetProperty("prizeAmount").GetInt64();
        var actualPrizeAmount = payload.GetProperty("actualPrizeAmount").GetInt64();
        var zoneCount = payload.GetProperty("zoneCount").GetInt32();
        if (zoneCount != ticket.ZoneCount || zoneCount != ticket.Zones.Count)
            throw new InvalidDataException("GameType 4 payload.zoneCount 與 scratch.zones 不一致。");

        var symbolsElement = payload.GetProperty("symbols");
        if (symbolsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("GameType 4 payload.symbols 必須是 array。");
        var symbols = symbolsElement.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString() ?? string.Empty
                : throw new InvalidDataException("GameType 4 payload.symbols 必須全部是字串。"))
            .ToArray();
        if (symbols.Length != zoneCount || symbols.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("GameType 4 payload.symbols 數量或內容無效。");

        var calculatedPrize = GameType4Rules.CalculateActualPrize(ticket, symbols);
        if (calculatedPrize != prizeAmount || calculatedPrize != actualPrizeAmount)
        {
            throw new InvalidDataException(
                "GameType 4 payload 的符號盤面、prizeAmount 與 actualPrizeAmount 不一致。");
        }

        return symbols
            .Select((symbol, index) => new GameType4RenderCell(index, ticket.Zones[index], symbol))
            .ToArray();
    }
}
