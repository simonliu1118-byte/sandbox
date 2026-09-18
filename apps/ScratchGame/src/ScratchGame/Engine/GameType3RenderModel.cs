using System.Text.Json;
using ScratchGame.Models;

namespace ScratchGame.Engine;

public sealed record GameType3RenderCell(
    int ZoneIndex,
    ScratchPackZone Zone,
    long Amount);

public static class GameType3RenderModel
{
    public static IReadOnlyList<GameType3RenderCell> Build(
        ScratchPackTicketDefinition ticket,
        JsonElement payload)
    {
        GameType3Rules.ValidateDefinition(ticket);

        if (payload.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("GameType 3 payload 必須是 JSON object。");
        if (!payload.TryGetProperty("gameType", out var gameType) || gameType.GetString() != "3")
            throw new InvalidDataException("GameType 3 Renderer 收到錯誤的 payload gameType。");

        var expectedPrize = RequiredInt64(payload, "prizeAmount");
        var actualPrize = RequiredInt64(payload, "actualPrizeAmount");
        var payloadZoneCount = RequiredInt32(payload, "zoneCount");
        var zoneCount = ticket.ZoneCount!.Value;
        if (expectedPrize < 0 || actualPrize < 0 || expectedPrize != actualPrize)
            throw new InvalidDataException("GameType 3 payload 的 prizeAmount / actualPrizeAmount 不一致或無效。");
        if (payloadZoneCount != zoneCount)
            throw new InvalidDataException("GameType 3 payload zoneCount 與 ScratchPack 定義不一致。");

        var amounts = ReadInt64Array(payload, "amounts");
        if (amounts.Length != zoneCount || amounts.Length != ticket.Zones.Count)
            throw new InvalidDataException("GameType 3 payload amount 數量與 scratch.zones 不一致。");

        var allowedAmounts = ticket.Prizes
            .Where(prize => prize.Count > 0)
            .Select(prize => prize.Amount)
            .ToHashSet();
        if (ticket.UseCustomDecoyAmounts == true && ticket.DecoyAmounts is not null)
            allowedAmounts.UnionWith(ticket.DecoyAmounts);

        if (amounts.Any(amount => amount <= 0 || !allowedAmounts.Contains(amount)))
            throw new InvalidDataException("GameType 3 payload 含未由正式 Prize Tier / decoyAmounts 定義的金額。");

        var groups = amounts
            .GroupBy(amount => amount)
            .Select(group => (Amount: group.Key, Count: group.Count()))
            .ToArray();
        if (groups.Any(group => group.Count > 3))
            throw new InvalidDataException("GameType 3 payload 含超過三個相同金額的非法結果。");

        var triples = groups.Where(group => group.Count == 3).ToArray();
        long calculatedPrize;
        if (triples.Length == 0)
        {
            calculatedPrize = 0;
        }
        else if (triples.Length == 1)
        {
            calculatedPrize = triples[0].Amount;
        }
        else
        {
            throw new InvalidDataException("GameType 3 payload 不可同時成立多組三個相同。");
        }

        if (calculatedPrize != expectedPrize)
            throw new InvalidDataException("GameType 3 payload 的三個相同結果與 Prize Tier 不一致。");
        if (expectedPrize > 0 && !ticket.Prizes.Any(prize => prize.Count > 0 && prize.Amount == expectedPrize))
            throw new InvalidDataException("GameType 3 payload 的中獎金額不是可發行的 Prize Tier。");

        return amounts
            .Select((amount, index) => new GameType3RenderCell(index, ticket.Zones[index], amount))
            .ToArray();
    }

    private static int RequiredInt32(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value) || !value.TryGetInt32(out var result))
            throw new InvalidDataException($"GameType 3 payload 缺少或無效的 {propertyName}。");
        return result;
    }

    private static long RequiredInt64(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value) || !value.TryGetInt64(out var result))
            throw new InvalidDataException($"GameType 3 payload 缺少或無效的 {propertyName}。");
        return result;
    }

    private static long[] ReadInt64Array(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"GameType 3 payload 缺少 {propertyName} array。");

        try
        {
            return value.EnumerateArray().Select(item => item.GetInt64()).ToArray();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"GameType 3 payload 的 {propertyName} 必須全部是整數。", ex);
        }
    }
}
