using System.Text.Json;
using ScratchGame.Models;

namespace ScratchGame.Engine;

public static class GamePayloadFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<long, int> BuiltInStarLine500PayoutToLineCount =
        new Dictionary<long, int>
        {
            [0] = 0,
            [100] = 1,
            [500] = 2,
            [1_000] = 3,
            [2_500] = 4,
            [5_000] = 5,
            [10_000] = 6,
            [100_000] = 8
        };

    public static string Create(string ruleId, long prizeAmount, long ticketPrice)
    {
        object payload = ruleId switch
        {
            // Legacy non-Pack route kept only for existing local data created before ScratchPack V1.
            "1" => CreateStarLine(
                prizeAmount,
                gridSize: 3,
                allowNearMissStars: true,
                payoutToLineCount: BuiltInStarLine500PayoutToLineCount,
                payloadRuleId: "ThreeLine"),
            "ThreeLine" => CreateLegacyThreeLine(prizeAmount),
            "LuckyNumberMatch" => CreateLuckyNumberMatch(prizeAmount),
            "MatchThree" => CreateMatchThree(prizeAmount),
            _ => throw new NotSupportedException($"不支援的 Game Rule：{ruleId}")
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string Create(ScratchPackTicketDefinition ticket, long prizeAmount)
    {
        object payload = ticket.GameType switch
        {
            "1" => CreateStarLineFromPack(ticket, prizeAmount),
            "2" => GameType2Rules.CreatePayload(ticket, prizeAmount),
            "3" => GameType3Rules.CreatePayload(ticket, prizeAmount),
            "4" => GameType4Rules.CreatePayload(ticket, prizeAmount),
            _ => throw new NotSupportedException($"ScratchPack 尚未實作 GameType：{ticket.GameType}")
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static object CreateStarLineFromPack(
        ScratchPackTicketDefinition ticket,
        long prizeAmount)
    {
        var orderedPrizes = ticket.Prizes.OrderBy(p => p.Amount).ToArray();
        var legalPositiveLineCounts = Enumerable.Range(1, 2 * ticket.GridSize)
            .Concat([2 * ticket.GridSize + 2])
            .ToArray();

        if (orderedPrizes.Length != legalPositiveLineCounts.Length)
        {
            throw new InvalidOperationException(
                $"GameType 1 / {ticket.GridSize}x{ticket.GridSize} Prize Tier 數量與合法連線數不一致。");
        }

        var payoutToLineCount = new Dictionary<long, int> { [0] = 0 };
        for (var i = 0; i < orderedPrizes.Length; i++)
            payoutToLineCount[orderedPrizes[i].Amount] = legalPositiveLineCounts[i];

        return CreateStarLine(
            prizeAmount,
            ticket.GridSize,
            ticket.AllowNearMiss,
            payoutToLineCount,
            payloadRuleId: "1");
    }

    private static object CreateStarLine(
        long prizeAmount,
        int gridSize,
        bool allowNearMissStars,
        IReadOnlyDictionary<long, int> payoutToLineCount,
        string payloadRuleId)
    {
        if (gridSize is < 3 or > 5)
            throw new ArgumentOutOfRangeException(nameof(gridSize), "GameType 1 只支援 3x3、4x4、5x5。");

        if (!payoutToLineCount.TryGetValue(prizeAmount, out var targetLineCount))
            throw new InvalidOperationException($"GameType 1 沒有定義獎金 ${prizeAmount:N0} 對應的連線數。");

        var lineMasks = BuildLineMasks(gridSize);
        var starMask = GenerateExactLineMask(gridSize, lineMasks, targetLineCount, allowNearMissStars);
        var actualLineCount = CountCompleteLines(starMask, lineMasks);
        if (actualLineCount != targetLineCount)
            throw new InvalidOperationException("GameType 1 產生的星星盤面連線數不一致。");

        var cells = Enumerable.Range(0, gridSize * gridSize)
            .Select(index => IsSet(starMask, index) ? "★" : $"D{index}-{Random.Shared.Next(1_000_000)}")
            .ToArray();

        return new
        {
            ruleId = payloadRuleId,
            gameType = "1",
            gridSize,
            prizeAmount,
            targetLineCount,
            actualLineCount,
            allowNearMissStars,
            cells
        };
    }

    private static ulong GenerateExactLineMask(
        int gridSize,
        IReadOnlyList<ulong> lineMasks,
        int targetLineCount,
        bool allowNearMissStars)
    {
        if (targetLineCount < 0 || targetLineCount > lineMasks.Count)
            throw new InvalidOperationException("GameType 1 的目標連線數超出合法範圍。");

        // 3x3 / 4x4 很小，直接枚舉所有星星組合可得到自然的 Near Miss 盤面。
        if (allowNearMissStars && gridSize <= 4)
        {
            var cellCount = gridSize * gridSize;
            var limit = 1UL << cellCount;
            var candidates = new List<ulong>();
            for (ulong mask = 0; mask < limit; mask++)
            {
                if (CountCompleteLines(mask, lineMasks) == targetLineCount)
                    candidates.Add(mask);
            }

            if (candidates.Count == 0)
                throw new InvalidOperationException($"GameType 1 無法產生 {gridSize}x{gridSize} 的 {targetLineCount} 線盤面。");

            return candidates[Random.Shared.Next(candidates.Count)];
        }

        // 一般模式只從完整中獎線的聯集建立盤面；這也是舊包缺少 Near Miss 參數時的穩定預設行為。
        var baseCandidates = new HashSet<ulong>();
        var subsetCount = 1 << lineMasks.Count;
        for (var subset = 0; subset < subsetCount; subset++)
        {
            ulong mask = 0;
            for (var lineIndex = 0; lineIndex < lineMasks.Count; lineIndex++)
            {
                if ((subset & (1 << lineIndex)) != 0)
                    mask |= lineMasks[lineIndex];
            }

            if (CountCompleteLines(mask, lineMasks) == targetLineCount)
                baseCandidates.Add(mask);
        }

        if (baseCandidates.Count == 0)
            throw new InvalidOperationException($"GameType 1 無法產生 {gridSize}x{gridSize} 的 {targetLineCount} 線盤面。");

        var selected = baseCandidates.ElementAt(Random.Shared.Next(baseCandidates.Count));
        if (!allowNearMissStars)
            return selected;

        // 5x5 不做 2^25 全枚舉；改以隨機加星、但每一步都驗證不增加實際中獎線數。
        var indexes = Enumerable.Range(0, gridSize * gridSize)
            .OrderBy(_ => Random.Shared.Next())
            .ToArray();
        foreach (var index in indexes)
        {
            if (IsSet(selected, index) || Random.Shared.NextDouble() > 0.6)
                continue;

            var candidate = selected | (1UL << index);
            if (CountCompleteLines(candidate, lineMasks) == targetLineCount)
                selected = candidate;
        }

        return selected;
    }

    private static IReadOnlyList<ulong> BuildLineMasks(int gridSize)
    {
        var result = new List<ulong>(gridSize * 2 + 2);

        for (var row = 0; row < gridSize; row++)
        {
            ulong mask = 0;
            for (var column = 0; column < gridSize; column++)
                mask |= 1UL << (row * gridSize + column);
            result.Add(mask);
        }

        for (var column = 0; column < gridSize; column++)
        {
            ulong mask = 0;
            for (var row = 0; row < gridSize; row++)
                mask |= 1UL << (row * gridSize + column);
            result.Add(mask);
        }

        ulong mainDiagonal = 0;
        ulong otherDiagonal = 0;
        for (var index = 0; index < gridSize; index++)
        {
            mainDiagonal |= 1UL << (index * gridSize + index);
            otherDiagonal |= 1UL << (index * gridSize + (gridSize - 1 - index));
        }
        result.Add(mainDiagonal);
        result.Add(otherDiagonal);

        return result;
    }

    private static int CountCompleteLines(ulong starMask, IReadOnlyList<ulong> lineMasks)
        => lineMasks.Count(lineMask => (starMask & lineMask) == lineMask);

    private static bool IsSet(ulong mask, int index)
        => (mask & (1UL << index)) != 0;

    private static object CreateLegacyThreeLine(long prizeAmount)
    {
        var lines = new[]
        {
            new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
            new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
            new[] { 0, 4, 8 }, new[] { 2, 4, 6 }
        };

        var cells = Enumerable.Range(1, 9).Select(i => i.ToString()).ToArray();
        int[]? winningLine = null;
        if (prizeAmount > 0)
        {
            winningLine = lines[Random.Shared.Next(lines.Length)];
            foreach (var index in winningLine)
                cells[index] = "★";
        }

        return new
        {
            ruleId = "ThreeLine",
            prizeAmount,
            cells,
            winningLine
        };
    }

    private static object CreateLuckyNumberMatch(long prizeAmount)
    {
        var pool = Enumerable.Range(1, 30).OrderBy(_ => Random.Shared.Next()).ToList();
        var winning = pool.Take(3).ToArray();
        var play = pool.Skip(3).Take(12).ToArray();

        if (prizeAmount > 0)
            play[Random.Shared.Next(play.Length)] = winning[Random.Shared.Next(winning.Length)];

        return new
        {
            ruleId = "LuckyNumberMatch",
            prizeAmount,
            winningNumbers = winning,
            playNumbers = play
        };
    }

    private static object CreateMatchThree(long prizeAmount)
    {
        var cells = Enumerable.Range(1, 9)
            .Select(i => $"S{i}")
            .ToArray();

        if (prizeAmount > 0)
        {
            var positions = Enumerable.Range(0, 9)
                .OrderBy(_ => Random.Shared.Next())
                .Take(3);
            foreach (var position in positions)
                cells[position] = "WIN";
        }

        return new
        {
            ruleId = "MatchThree",
            prizeAmount,
            cells
        };
    }
}
