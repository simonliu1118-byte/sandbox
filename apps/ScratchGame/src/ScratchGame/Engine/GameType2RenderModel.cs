using System.Text.Json;
using ScratchGame.Models;

namespace ScratchGame.Engine;

public sealed record GameType2RenderCell(
    int ZoneIndex,
    ScratchPackZone Zone,
    bool IsWinningSide,
    long Number,
    long? PrizeAmount);

public static class GameType2RenderModel
{
    public static IReadOnlyList<GameType2RenderCell> Build(
        ScratchPackTicketDefinition ticket,
        JsonElement payload)
    {
        GameType2Rules.ValidateDefinition(ticket);

        if (payload.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("GameType 2 payload 必須是 JSON object。");
        if (!payload.TryGetProperty("gameType", out var gameType) || gameType.GetString() != "2")
            throw new InvalidDataException("GameType 2 Renderer 收到錯誤的 payload gameType。");

        var payoutSource = payload.GetProperty("payoutSource").GetString();
        if (payoutSource != ticket.PayoutSource)
            throw new InvalidDataException("GameType 2 payload payoutSource 與 ScratchPack 定義不一致。");

        var expectedPrize = RequiredInt64(payload, "prizeAmount");
        var actualPrize = RequiredInt64(payload, "actualPrizeAmount");
        if (expectedPrize < 0 || actualPrize < 0 || expectedPrize != actualPrize)
            throw new InvalidDataException("GameType 2 payload 的 prizeAmount / actualPrizeAmount 不一致或無效。");

        var winningNumbers = ReadInt64Array(payload, "winningNumbers");
        var playNumbers = ReadInt64Array(payload, "playNumbers");
        var winningAmounts = ReadInt64Array(payload, "winningPrizeAmounts");
        var playAmounts = ReadInt64Array(payload, "playPrizeAmounts");

        var winningCount = ticket.WinningNumberCount!.Value;
        var playCount = ticket.PlayNumberCount!.Value;
        var numberMin = ticket.NumberMin!.Value;
        var numberMax = ticket.NumberMax!.Value;
        if (winningNumbers.Length != winningCount || playNumbers.Length != playCount)
            throw new InvalidDataException("GameType 2 payload 號碼數量與 ScratchPack 定義不一致。");

        if (winningNumbers.Distinct().Count() != winningNumbers.Length ||
            playNumbers.Distinct().Count() != playNumbers.Length)
        {
            throw new InvalidDataException("GameType 2 payload 的同一組號碼不得重複。");
        }

        if (winningNumbers.Any(number => number < numberMin || number > numberMax) ||
            playNumbers.Any(number => number < numberMin || number > numberMax))
        {
            throw new InvalidDataException("GameType 2 payload 含超出 numberMin / numberMax 的號碼。");
        }

        var allowedDisplayAmounts = ticket.DisplayPrizeAmounts!.ToHashSet();
        if (payoutSource == "winning")
        {
            ValidatePrizeSide(
                winningAmounts,
                winningCount,
                "winning",
                allowedDisplayAmounts,
                ticket.AllowPrizeAmountRepeat!.Value);
            if (playAmounts.Length != 0)
                throw new InvalidDataException("payoutSource=winning 時 playPrizeAmounts 必須為空。");
        }
        else
        {
            ValidatePrizeSide(
                playAmounts,
                playCount,
                "play",
                allowedDisplayAmounts,
                ticket.AllowPrizeAmountRepeat!.Value);
            if (winningAmounts.Length != 0)
                throw new InvalidDataException("payoutSource=play 時 winningPrizeAmounts 必須為空。");
        }

        var winningSet = winningNumbers.ToHashSet();
        var playSet = playNumbers.ToHashSet();
        var calculatedPrize = payoutSource == "winning"
            ? winningNumbers
                .Select((number, index) => (number, index))
                .Where(item => playSet.Contains(item.number))
                .Sum(item => winningAmounts[item.index])
            : playNumbers
                .Select((number, index) => (number, index))
                .Where(item => winningSet.Contains(item.number))
                .Sum(item => playAmounts[item.index]);
        if (calculatedPrize != expectedPrize)
            throw new InvalidDataException("GameType 2 payload 的命中格獎金加總與 Prize Tier 不一致。");

        var cells = new List<GameType2RenderCell>(ticket.Zones.Count);
        for (var index = 0; index < winningCount; index++)
        {
            cells.Add(new GameType2RenderCell(
                index,
                ticket.Zones[index],
                IsWinningSide: true,
                winningNumbers[index],
                payoutSource == "winning" ? winningAmounts[index] : null));
        }

        for (var index = 0; index < playCount; index++)
        {
            var zoneIndex = winningCount + index;
            cells.Add(new GameType2RenderCell(
                zoneIndex,
                ticket.Zones[zoneIndex],
                IsWinningSide: false,
                playNumbers[index],
                payoutSource == "play" ? playAmounts[index] : null));
        }

        if (cells.Count != ticket.Zones.Count)
            throw new InvalidDataException("GameType 2 Renderer cell 數量與 scratch.zones 不一致。");

        return cells;
    }

    private static long RequiredInt64(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value) || !value.TryGetInt64(out var result))
            throw new InvalidDataException($"GameType 2 payload 缺少或無效的 {propertyName}。");
        return result;
    }

    private static long[] ReadInt64Array(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"GameType 2 payload 缺少 {propertyName} array。");

        try
        {
            return value.EnumerateArray().Select(item => item.GetInt64()).ToArray();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"GameType 2 payload 的 {propertyName} 必須全部是整數。", ex);
        }
    }

    private static void ValidatePrizeSide(
        long[] amounts,
        int expectedCount,
        string side,
        IReadOnlySet<long> allowedDisplayAmounts,
        bool allowRepeat)
    {
        if (amounts.Length != expectedCount)
            throw new InvalidDataException($"GameType 2 {side} prize amount 數量與格數不一致。");
        if (amounts.Any(amount => amount <= 0 || !allowedDisplayAmounts.Contains(amount)))
        {
            throw new InvalidDataException(
                $"GameType 2 {side} prize amount 必須來自 displayPrizeAmounts 且全部 > 0。");
        }
        if (!allowRepeat && amounts.Distinct().Count() != amounts.Length)
            throw new InvalidDataException($"GameType 2 {side} prize amount 不允許重複。");
    }
}
