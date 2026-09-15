using System.Text.Json;
using ScratchGame.Models;

namespace ScratchGame.Engine;

public static class GamePayloadFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Create(ScratchPackTicketDefinition definition, long prizeAmount)
    {
        object payload = definition.GameType switch
        {
            "1" => CreateGameType1(definition, prizeAmount),
            _ => throw new NotSupportedException($"目前 runtime 尚未實作 GameType：{definition.GameType}")
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    // Legacy entry point retained only so an existing local database does not fail before migration.
    // New ScratchPack V1 tickets must call the definition-based overload above.
    public static string Create(string ruleId, long prizeAmount, long ticketPrice)
    {
        object payload = ruleId switch
        {
            "ThreeLine" => CreateLegacyThreeLine(prizeAmount),
            "LuckyNumberMatch" => CreateLuckyNumberMatch(prizeAmount),
            "MatchThree" => CreateMatchThree(prizeAmount),
            _ => throw new NotSupportedException($"不支援的舊版 Game Rule：{ruleId}")
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static object CreateGameType1(ScratchPackTicketDefinition definition, long prizeAmount)
    {
        var n = definition.GridSize;
        var lineSets = BuildLineSets(n);
        ulong starMask;
        var targetLineCount = 0;

        if (prizeAmount > 0)
        {
            var orderedPrizes = definition.Prizes.OrderBy(p => p.Amount).ToArray();
            var tierIndex = Array.FindIndex(orderedPrizes, p => p.Amount == prizeAmount);
            if (tierIndex < 0)
                throw new InvalidOperationException($"獎金 {prizeAmount} 不存在於目前 ScratchPack Prize Tier。");

            var legalLineCounts = Enumerable.Range(1, 2 * n)
                .Append(2 * n + 2)
                .ToArray();
            targetLineCount = legalLineCounts[tierIndex];
            starMask = CreateExactLineMask(n, lineSets, targetLineCount);
        }
        else
        {
            starMask = definition.AllowNearMiss
                ? CreateNearMissMask(n, lineSets)
                : 0UL;
        }

        var cells = Enumerable.Range(0, n * n)
            .Select(index => (starMask & (1UL << index)) != 0)
            .ToArray();

        return new
        {
            gameType = "1",
            prizeAmount,
            lineCount = targetLineCount,
            cells
        };
    }

    private static IReadOnlyList<ulong> BuildLineSets(int n)
    {
        var lines = new List<ulong>();
        for (var row = 0; row < n; row++)
        {
            ulong mask = 0;
            for (var col = 0; col < n; col++)
                mask |= 1UL << (row * n + col);
            lines.Add(mask);
        }

        for (var col = 0; col < n; col++)
        {
            ulong mask = 0;
            for (var row = 0; row < n; row++)
                mask |= 1UL << (row * n + col);
            lines.Add(mask);
        }

        ulong diagonalA = 0;
        ulong diagonalB = 0;
        for (var i = 0; i < n; i++)
        {
            diagonalA |= 1UL << (i * n + i);
            diagonalB |= 1UL << (i * n + (n - 1 - i));
        }
        lines.Add(diagonalA);
        lines.Add(diagonalB);
        return lines;
    }

    private static ulong CreateExactLineMask(int n, IReadOnlyList<ulong> lines, int targetLineCount)
    {
        var candidates = new List<ulong>();
        var subsetCount = 1 << lines.Count;
        for (var subset = 1; subset < subsetCount; subset++)
        {
            ulong cells = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                if ((subset & (1 << i)) != 0)
                    cells |= lines[i];
            }

            if (CountCompletedLines(cells, lines) == targetLineCount)
                candidates.Add(cells);
        }

        if (candidates.Count == 0)
            throw new InvalidOperationException($"GameType 1 無法產生 {n}x{n} 的 {targetLineCount} 線盤面。");
        return candidates[Random.Shared.Next(candidates.Count)];
    }

    private static ulong CreateNearMissMask(int n, IReadOnlyList<ulong> lines)
    {
        var targetLine = lines[Random.Shared.Next(lines.Count)];
        var lineCells = Enumerable.Range(0, n * n)
            .Where(index => (targetLine & (1UL << index)) != 0)
            .OrderBy(_ => Random.Shared.Next())
            .Take(Math.Max(0, n - 1))
            .ToArray();

        ulong result = 0;
        foreach (var index in lineCells)
            result |= 1UL << index;
        return CountCompletedLines(result, lines) == 0 ? result : 0UL;
    }

    private static int CountCompletedLines(ulong cells, IReadOnlyList<ulong> lines)
        => lines.Count(line => (cells & line) == line);

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

        return new { ruleId = "ThreeLine", prizeAmount, cells, winningLine };
    }

    private static object CreateLuckyNumberMatch(long prizeAmount)
    {
        var pool = Enumerable.Range(1, 30).OrderBy(_ => Random.Shared.Next()).ToList();
        var winning = pool.Take(3).ToArray();
        var play = pool.Skip(3).Take(12).ToArray();
        if (prizeAmount > 0)
            play[Random.Shared.Next(play.Length)] = winning[Random.Shared.Next(winning.Length)];
        return new { ruleId = "LuckyNumberMatch", prizeAmount, winningNumbers = winning, playNumbers = play };
    }

    private static object CreateMatchThree(long prizeAmount)
    {
        var cells = Enumerable.Range(1, 9).Select(i => $"S{i}").ToArray();
        if (prizeAmount > 0)
        {
            var positions = Enumerable.Range(0, 9).OrderBy(_ => Random.Shared.Next()).Take(3);
            foreach (var position in positions)
                cells[position] = "WIN";
        }
        return new { ruleId = "MatchThree", prizeAmount, cells };
    }
}
