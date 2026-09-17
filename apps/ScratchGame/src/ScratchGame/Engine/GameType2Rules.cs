using ScratchGame.Models;

namespace ScratchGame.Engine;

public static class GameType2Rules
{
    public static void ValidateDefinition(ScratchPackTicketDefinition ticket)
    {
        if (ticket.GameType != "2")
            throw new InvalidDataException("GameType2Rules 只能驗證 GameType 2。");

        var winningCount = RequirePositive(ticket.WinningNumberCount, "winningNumberCount");
        var playCount = RequirePositive(ticket.PlayNumberCount, "playNumberCount");
        var numberMin = ticket.NumberMin ?? throw new InvalidDataException("GameType 2 缺少 numberMin。");
        var numberMax = ticket.NumberMax ?? throw new InvalidDataException("GameType 2 缺少 numberMax。");
        if (numberMin >= numberMax)
            throw new InvalidDataException("GameType 2 必須 numberMin < numberMax。");

        var payoutSource = ticket.PayoutSource ?? throw new InvalidDataException("GameType 2 缺少 payoutSource。");
        if (payoutSource is not "play" and not "winning")
            throw new InvalidDataException("GameType 2 payoutSource 只允許 play 或 winning。");

        if (ticket.AllowPrizeAmountRepeat is null)
            throw new InvalidDataException("GameType 2 缺少 allowPrizeAmountRepeat。");

        var displayAmounts = ticket.DisplayPrizeAmounts?.ToArray()
            ?? throw new InvalidDataException("GameType 2 缺少 displayPrizeAmounts。");
        if (displayAmounts.Length == 0)
            throw new InvalidDataException("GameType 2 displayPrizeAmounts 不可為空。");
        if (displayAmounts.Any(amount => amount <= 0))
            throw new InvalidDataException("GameType 2 displayPrizeAmounts 必須全部 > 0。");
        if (displayAmounts.Distinct().Count() != displayAmounts.Length)
            throw new InvalidDataException("GameType 2 displayPrizeAmounts 不得重複。");

        if (ticket.Zones.Count != winningCount + playCount)
        {
            throw new InvalidDataException(
                $"GameType 2 必須精確包含 {winningCount + playCount} 個 scratch zones：" +
                $"前 {winningCount} 個為中獎號碼，後 {playCount} 個為你的號碼。");
        }

        if (ticket.Zones.Select(zone => zone.Id).Distinct(StringComparer.Ordinal).Count() != ticket.Zones.Count)
            throw new InvalidDataException("scratch zone id 不得重複。");

        var first = ticket.Zones[0];
        if (ticket.Zones.Any(zone =>
                zone.Width != first.Width || zone.Height != first.Height || zone.Shape != first.Shape ||
                zone.CornerRadius != first.CornerRadius))
        {
            throw new InvalidDataException(
                "GameType 2 所有 scratch zones 的尺寸、shape 與 cornerRadius 必須一致，以符合固定 Renderer 模板。");
        }

        var rangeSize = (long)numberMax - numberMin + 1L;
        if (rangeSize < Math.Max(winningCount, playCount))
            throw new InvalidDataException("GameType 2 數字範圍不足以讓同一組內的號碼保持唯一。");

        var prizeBearingCount = payoutSource == "play" ? playCount : winningCount;
        var allowRepeat = ticket.AllowPrizeAmountRepeat.Value;
        if (!allowRepeat && displayAmounts.Length < prizeBearingCount)
        {
            throw new InvalidDataException(
                "allowPrizeAmountRepeat=false 時，displayPrizeAmounts 種類數必須至少等於有獎金一側的格數。");
        }

        var totalWinningTickets = ticket.Prizes.Sum(prize => prize.Count);
        if (ticket.IssueSize > totalWinningTickets && rangeSize < (long)winningCount + playCount)
        {
            throw new InvalidDataException(
                "GameType 2 含未中獎票時，數字範圍必須足以生成兩組完全不重疊的號碼。");
        }

        foreach (var prize in ticket.Prizes)
        {
            if (!TryFindPrizeCombination(ticket, prize.Amount, out _))
            {
                throw new InvalidDataException(
                    $"GameType 2 Prize Tier ${prize.Amount:N0} 無法由目前 displayPrizeAmounts、" +
                    "命中數上限、重複規則與數字範圍精確生成。");
            }
        }
    }

    public static object CreatePayload(ScratchPackTicketDefinition ticket, long prizeAmount)
    {
        ValidateDefinition(ticket);

        var winningCount = ticket.WinningNumberCount!.Value;
        var playCount = ticket.PlayNumberCount!.Value;
        var numberMin = ticket.NumberMin!.Value;
        var numberMax = ticket.NumberMax!.Value;
        var payoutSource = ticket.PayoutSource!;
        var displayAmounts = ticket.DisplayPrizeAmounts!.ToArray();
        var allowRepeat = ticket.AllowPrizeAmountRepeat!.Value;

        long[] winningAmounts;
        long[] playAmounts;
        long[] winningNumbers;
        long[] playNumbers;

        if (prizeAmount == 0)
        {
            var rangeSize = (long)numberMax - numberMin + 1L;
            if (rangeSize < (long)winningCount + playCount)
                throw new InvalidOperationException("GameType 2 無法生成零命中的未中獎盤面。");

            (winningNumbers, playNumbers) = GenerateNumbers(
                numberMin, numberMax, winningCount, playCount, matchCount: 0);

            winningAmounts = payoutSource == "winning"
                ? FillAllPrizeAmounts(displayAmounts, winningCount, allowRepeat)
                : Array.Empty<long>();
            playAmounts = payoutSource == "play"
                ? FillAllPrizeAmounts(displayAmounts, playCount, allowRepeat)
                : Array.Empty<long>();
        }
        else
        {
            if (!TryFindPrizeCombination(ticket, prizeAmount, out var matchedAmounts))
                throw new InvalidOperationException($"GameType 2 無法生成獎金 ${prizeAmount:N0} 的合法盤面。");

            (winningNumbers, playNumbers) = GenerateNumbers(
                numberMin, numberMax, winningCount, playCount, matchedAmounts.Length);

            var winningSet = winningNumbers.ToHashSet();
            var playSet = playNumbers.ToHashSet();
            var matchedWinningIndexes = winningNumbers
                .Select((number, index) => (number, index))
                .Where(item => playSet.Contains(item.number))
                .Select(item => item.index)
                .ToArray();
            var matchedPlayIndexes = playNumbers
                .Select((number, index) => (number, index))
                .Where(item => winningSet.Contains(item.number))
                .Select(item => item.index)
                .ToArray();

            winningAmounts = payoutSource == "winning"
                ? BuildPrizeAmounts(displayAmounts, winningCount, matchedWinningIndexes, matchedAmounts, allowRepeat)
                : Array.Empty<long>();
            playAmounts = payoutSource == "play"
                ? BuildPrizeAmounts(displayAmounts, playCount, matchedPlayIndexes, matchedAmounts, allowRepeat)
                : Array.Empty<long>();
        }

        var finalWinningSet = winningNumbers.ToHashSet();
        var finalPlaySet = playNumbers.ToHashSet();
        var finalMatchedWinningIndexes = winningNumbers
            .Select((number, index) => (number, index))
            .Where(item => finalPlaySet.Contains(item.number))
            .Select(item => item.index)
            .ToArray();
        var finalMatchedPlayIndexes = playNumbers
            .Select((number, index) => (number, index))
            .Where(item => finalWinningSet.Contains(item.number))
            .Select(item => item.index)
            .ToArray();

        var actualPrizeAmount = payoutSource == "winning"
            ? finalMatchedWinningIndexes.Sum(index => winningAmounts[index])
            : finalMatchedPlayIndexes.Sum(index => playAmounts[index]);
        if (actualPrizeAmount != prizeAmount)
            throw new InvalidOperationException("GameType 2 生成盤面的實際命中獎金與目標 Prize Tier 不一致。");

        return new
        {
            ruleId = "2",
            gameType = "2",
            prizeAmount,
            actualPrizeAmount,
            payoutSource,
            allowPrizeAmountRepeat = allowRepeat,
            winningNumbers,
            playNumbers,
            winningPrizeAmounts = winningAmounts,
            playPrizeAmounts = playAmounts,
            matchedWinningIndexes = finalMatchedWinningIndexes,
            matchedPlayIndexes = finalMatchedPlayIndexes
        };
    }

    public static bool TryFindPrizeCombination(
        ScratchPackTicketDefinition ticket,
        long targetAmount,
        out long[] combination)
    {
        combination = Array.Empty<long>();
        if (targetAmount <= 0 || ticket.GameType != "2" ||
            ticket.WinningNumberCount is null || ticket.PlayNumberCount is null ||
            ticket.NumberMin is null || ticket.NumberMax is null ||
            ticket.AllowPrizeAmountRepeat is null || ticket.DisplayPrizeAmounts is null)
        {
            return false;
        }

        var values = ticket.DisplayPrizeAmounts.OrderBy(value => value).ToArray();
        var allowRepeat = ticket.AllowPrizeAmountRepeat.Value;
        var maxMatches = Math.Min(ticket.WinningNumberCount.Value, ticket.PlayNumberCount.Value);
        var rangeSize = (long)ticket.NumberMax.Value - ticket.NumberMin.Value + 1L;

        for (var count = 1; count <= maxMatches; count++)
        {
            if (rangeSize < (long)ticket.WinningNumberCount.Value + ticket.PlayNumberCount.Value - count)
                continue;
            if (!allowRepeat && count > values.Length)
                continue;

            var selected = new List<long>(count);
            var failed = new HashSet<(int Start, int Slots, long Remaining)>();
            if (FindCombination(values, allowRepeat, startIndex: 0, count, targetAmount, selected, failed))
            {
                combination = selected.ToArray();
                return true;
            }
        }

        return false;
    }

    private static bool FindCombination(
        IReadOnlyList<long> values,
        bool allowRepeat,
        int startIndex,
        int slots,
        long remaining,
        List<long> selected,
        HashSet<(int Start, int Slots, long Remaining)> failed)
    {
        if (slots == 0)
            return remaining == 0;
        if (remaining <= 0 || startIndex >= values.Count)
            return false;

        var key = (startIndex, slots, remaining);
        if (failed.Contains(key))
            return false;

        for (var index = startIndex; index < values.Count; index++)
        {
            var value = values[index];
            if (value > remaining)
                break;

            selected.Add(value);
            var nextStart = allowRepeat ? index : index + 1;
            if (FindCombination(values, allowRepeat, nextStart, slots - 1, remaining - value, selected, failed))
                return true;
            selected.RemoveAt(selected.Count - 1);
        }

        failed.Add(key);
        return false;
    }

    private static (long[] Winning, long[] Play) GenerateNumbers(
        int numberMin,
        int numberMax,
        int winningCount,
        int playCount,
        int matchCount)
    {
        var totalDistinct = winningCount + playCount - matchCount;
        var sampled = SampleDistinct(numberMin, numberMax, totalDistinct);
        var shared = sampled.Take(matchCount).ToArray();
        var winningOnly = sampled.Skip(matchCount).Take(winningCount - matchCount).ToArray();
        var playOnly = sampled.Skip(matchCount + winningCount - matchCount).Take(playCount - matchCount).ToArray();

        var winning = shared.Concat(winningOnly).ToArray();
        var play = shared.Concat(playOnly).ToArray();
        Shuffle(winning);
        Shuffle(play);
        return (winning, play);
    }

    private static long[] BuildPrizeAmounts(
        IReadOnlyList<long> displayAmounts,
        int cellCount,
        IReadOnlyList<int> matchedIndexes,
        IReadOnlyList<long> matchedAmounts,
        bool allowRepeat)
    {
        if (matchedIndexes.Count != matchedAmounts.Count)
            throw new InvalidOperationException("GameType 2 命中格數與命中金額數量不一致。");

        var result = new long[cellCount];
        var amountOrder = matchedAmounts.ToArray();
        Shuffle(amountOrder);
        for (var i = 0; i < matchedIndexes.Count; i++)
            result[matchedIndexes[i]] = amountOrder[i];

        var unfilled = Enumerable.Range(0, cellCount).Where(index => result[index] == 0).ToArray();
        if (allowRepeat)
        {
            foreach (var index in unfilled)
                result[index] = displayAmounts[Random.Shared.Next(displayAmounts.Count)];
            return result;
        }

        var used = matchedAmounts.ToHashSet();
        var remaining = displayAmounts.Where(amount => !used.Contains(amount)).ToArray();
        Shuffle(remaining);
        if (remaining.Length < unfilled.Length)
            throw new InvalidOperationException("GameType 2 無法在禁止獎金重複的條件下填滿所有獎金格。");
        for (var i = 0; i < unfilled.Length; i++)
            result[unfilled[i]] = remaining[i];
        return result;
    }

    private static long[] FillAllPrizeAmounts(
        IReadOnlyList<long> displayAmounts,
        int count,
        bool allowRepeat)
    {
        if (allowRepeat)
        {
            return Enumerable.Range(0, count)
                .Select(_ => displayAmounts[Random.Shared.Next(displayAmounts.Count)])
                .ToArray();
        }

        var values = displayAmounts.ToArray();
        Shuffle(values);
        if (values.Length < count)
            throw new InvalidOperationException("GameType 2 displayPrizeAmounts 不足以填滿不重複的獎金格。");
        return values.Take(count).ToArray();
    }

    private static long[] SampleDistinct(int min, int max, int count)
    {
        var rangeSize = (long)max - min + 1L;
        if (count < 0 || rangeSize < count)
            throw new InvalidOperationException("GameType 2 數字範圍不足以產生指定數量的唯一號碼。");

        var selected = new HashSet<long>();
        while (selected.Count < count)
            selected.Add(Random.Shared.NextInt64(min, (long)max + 1L));
        var result = selected.ToArray();
        Shuffle(result);
        return result;
    }

    private static int RequirePositive(int? value, string name)
    {
        if (value is null || value <= 0)
            throw new InvalidDataException($"GameType 2 {name} 必須 > 0。");
        return value.Value;
    }

    private static void Shuffle<T>(T[] values)
    {
        Random.Shared.Shuffle(values);
    }
}
