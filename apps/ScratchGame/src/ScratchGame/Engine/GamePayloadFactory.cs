using System.Text.Json;

namespace ScratchGame.Engine;

public static class GamePayloadFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Create(string ruleId, long prizeAmount, long ticketPrice)
    {
        object payload = ruleId switch
        {
            "ThreeLine" => CreateThreeLine(prizeAmount),
            "LuckyNumberMatch" => CreateLuckyNumberMatch(prizeAmount),
            "MatchThree" => CreateMatchThree(prizeAmount),
            _ => throw new NotSupportedException($"不支援的 Game Rule：{ruleId}")
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static object CreateThreeLine(long prizeAmount)
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
