using ScratchGame.Models;

namespace ScratchGame.Engine;

public static class GameType3Rules
{
    public const int MinZoneCount = 3;
    public const int MaxZoneCount = 25;
    public const int DefaultNearMissPairProbability = 75;
    public const int DefaultNearMissPairCount = 1;

    public static void ValidateDefinition(ScratchPackTicketDefinition ticket)
    {
        if (ticket.GameType != "3")
            throw new InvalidDataException("GameType3Rules 只能驗證 GameType 3。");

        var zoneCount = ticket.ZoneCount
            ?? throw new InvalidDataException("GameType 3 缺少 zoneCount。");
        if (zoneCount is < MinZoneCount or > MaxZoneCount)
        {
            throw new InvalidDataException(
                $"GameType 3 zoneCount 只允許 {MinZoneCount}～{MaxZoneCount}。");
        }

        if (ticket.Zones.Count != zoneCount)
            throw new InvalidDataException($"GameType 3 必須精確包含 {zoneCount} 個 scratch zones。");
        if (ticket.Zones.Select(zone => zone.Id).Distinct(StringComparer.Ordinal).Count() != ticket.Zones.Count)
            throw new InvalidDataException("scratch zone id 不得重複。");

        var first = ticket.Zones[0];
        if (ticket.Zones.Any(zone =>
                zone.Width != first.Width || zone.Height != first.Height || zone.Shape != first.Shape ||
                zone.CornerRadius != first.CornerRadius))
        {
            throw new InvalidDataException(
                "GameType 3 所有 scratch zones 的尺寸、shape 與 cornerRadius 必須一致，以符合固定 Renderer 模板。");
        }

        var useCustomDecoys = ticket.UseCustomDecoyAmounts ?? false;
        var decoyAmounts = ticket.DecoyAmounts?.ToArray() ?? Array.Empty<long>();
        if (!useCustomDecoys && decoyAmounts.Length > 0)
            throw new InvalidDataException("useCustomDecoyAmounts=false 時不得提供 decoyAmounts。");
        if (useCustomDecoys && decoyAmounts.Length == 0)
            throw new InvalidDataException("useCustomDecoyAmounts=true 時必須提供至少一個 decoyAmounts。");
        if (decoyAmounts.Any(amount => amount <= 0))
            throw new InvalidDataException("GameType 3 decoyAmounts 必須全部 > 0。");
        if (decoyAmounts.Distinct().Count() != decoyAmounts.Length)
            throw new InvalidDataException("GameType 3 decoyAmounts 不得重複。");

        var prizeAmounts = ticket.Prizes.Select(prize => prize.Amount).ToHashSet();
        if (decoyAmounts.Any(prizeAmounts.Contains))
            throw new InvalidDataException("GameType 3 decoyAmounts 不得與任何正式 Prize Tier 金額重複。");

        var nearMissProbability = ticket.NearMissPairProbability ?? DefaultNearMissPairProbability;
        if (nearMissProbability is < 0 or > 100)
            throw new InvalidDataException("GameType 3 nearMissPairProbability 必須介於 0～100。");

        var nearMissPairCount = ticket.NearMissPairCount ?? DefaultNearMissPairCount;
        if (nearMissPairCount <= 0)
            throw new InvalidDataException("GameType 3 nearMissPairCount 必須大於 0。");
        if (nearMissPairCount * 2 > zoneCount)
            throw new InvalidDataException("GameType 3 nearMissPairCount 超過 zoneCount 可容納的雙數組數。");

        var usableAmounts = GetUsableAmounts(ticket).ToArray();
        var totalWinningTickets = ticket.Prizes.Sum(prize => prize.Count);
        var losingTickets = ticket.IssueSize - totalWinningTickets;

        if (losingTickets > 0)
        {
            ValidateFillerCapacity(
                usableAmounts,
                zoneCount,
                nearMissProbability,
                nearMissPairCount,
                "未中獎票");
        }

        foreach (var prize in ticket.Prizes.Where(prize => prize.Count > 0))
        {
            var fillers = usableAmounts.Where(amount => amount != prize.Amount).ToArray();
            ValidateFillerCapacity(
                fillers,
                zoneCount - 3,
                nearMissProbability,
                nearMissPairCount,
                $"Prize Tier ${prize.Amount:N0}");
        }
    }

    public static object CreatePayload(ScratchPackTicketDefinition ticket, long prizeAmount)
    {
        ValidateDefinition(ticket);

        var zoneCount = ticket.ZoneCount!.Value;
        var totalWinningTickets = ticket.Prizes.Sum(prize => prize.Count);
        var losingTickets = ticket.IssueSize - totalWinningTickets;

        if (prizeAmount == 0)
        {
            if (losingTickets <= 0)
                throw new InvalidOperationException("GameType 3 此 Pack 沒有未中獎票，不能生成 prizeAmount=0。");
        }
        else if (!ticket.Prizes.Any(prize => prize.Amount == prizeAmount && prize.Count > 0))
        {
            throw new InvalidOperationException($"GameType 3 沒有可發行的 Prize Tier ${prizeAmount:N0}。");
        }

        var amounts = new List<long>(zoneCount);
        if (prizeAmount > 0)
        {
            amounts.Add(prizeAmount);
            amounts.Add(prizeAmount);
            amounts.Add(prizeAmount);
        }

        var fillerSlots = zoneCount - amounts.Count;
        var availableFillers = GetUsableAmounts(ticket)
            .Where(amount => amount != prizeAmount)
            .ToArray();

        var nearMissProbability = ticket.NearMissPairProbability ?? DefaultNearMissPairProbability;
        var nearMissPairCount = ticket.NearMissPairCount ?? DefaultNearMissPairCount;
        var forceNearMiss = fillerSlots > 0 && nearMissProbability > 0 &&
                            Random.Shared.Next(100) < nearMissProbability;

        amounts.AddRange(BuildFillers(
            availableFillers,
            fillerSlots,
            forceNearMiss ? nearMissPairCount : 0));

        var shuffled = amounts.OrderBy(_ => Random.Shared.Next()).ToArray();
        ValidateGeneratedOutcome(shuffled, prizeAmount);

        var actualPrizeAmount = shuffled
            .GroupBy(amount => amount)
            .Where(group => group.Count() == 3)
            .Select(group => group.Key)
            .SingleOrDefault();

        return new
        {
            ruleId = "3",
            gameType = "3",
            prizeAmount,
            actualPrizeAmount,
            zoneCount,
            amounts = shuffled
        };
    }

    private static IReadOnlyList<long> GetUsableAmounts(ScratchPackTicketDefinition ticket)
    {
        var result = ticket.Prizes
            .Where(prize => prize.Count > 0)
            .Select(prize => prize.Amount)
            .ToList();

        if (ticket.UseCustomDecoyAmounts == true && ticket.DecoyAmounts is not null)
            result.AddRange(ticket.DecoyAmounts);

        return result.Distinct().ToArray();
    }

    private static void ValidateFillerCapacity(
        IReadOnlyCollection<long> availableAmounts,
        int fillerSlots,
        int nearMissProbability,
        int nearMissPairCount,
        string label)
    {
        if (fillerSlots < 0)
            throw new InvalidDataException($"GameType 3 {label} 的可用格數不足以放置三個相同金額。");
        if ((long)availableAmounts.Count * 2 < fillerSlots)
        {
            throw new InvalidDataException(
                $"GameType 3 {label} 可用的非中獎金額種類不足；每種最多只能出現兩次。");
        }

        if (nearMissProbability <= 0)
            return;

        if (nearMissPairCount * 2 > fillerSlots)
        {
            throw new InvalidDataException(
                $"GameType 3 {label} 無法容納 nearMissPairCount={nearMissPairCount} 的差一個成獎組合。");
        }
        if (availableAmounts.Count < nearMissPairCount)
        {
            throw new InvalidDataException(
                $"GameType 3 {label} 可用金額種類不足以建立 {nearMissPairCount} 組 Near Miss Pair。");
        }
    }

    private static IReadOnlyList<long> BuildFillers(
        IReadOnlyList<long> availableAmounts,
        int fillerSlots,
        int forcedPairCount)
    {
        if (fillerSlots == 0)
            return Array.Empty<long>();

        var capacities = availableAmounts.ToDictionary(amount => amount, _ => 2);
        var result = new List<long>(fillerSlots);

        if (forcedPairCount > 0)
        {
            var forcedPairs = availableAmounts
                .OrderBy(_ => Random.Shared.Next())
                .Take(forcedPairCount)
                .ToArray();
            if (forcedPairs.Length != forcedPairCount || forcedPairCount * 2 > fillerSlots)
                throw new InvalidOperationException("GameType 3 無法建立要求的 Near Miss Pair。");

            foreach (var amount in forcedPairs)
            {
                result.Add(amount);
                result.Add(amount);
                capacities[amount] = 0;
            }
        }

        var remaining = fillerSlots - result.Count;
        var bag = capacities
            .SelectMany(pair => Enumerable.Repeat(pair.Key, pair.Value))
            .OrderBy(_ => Random.Shared.Next())
            .ToArray();
        if (bag.Length < remaining)
            throw new InvalidOperationException("GameType 3 可用的非中獎金額不足以填滿盤面。");

        result.AddRange(bag.Take(remaining));
        return result;
    }

    private static void ValidateGeneratedOutcome(IReadOnlyList<long> amounts, long expectedPrizeAmount)
    {
        var groups = amounts
            .GroupBy(amount => amount)
            .Select(group => (Amount: group.Key, Count: group.Count()))
            .ToArray();

        if (groups.Any(group => group.Count > 3))
            throw new InvalidOperationException("GameType 3 產生了超過三個相同金額的非法盤面。");

        var triples = groups.Where(group => group.Count == 3).ToArray();
        if (expectedPrizeAmount == 0)
        {
            if (triples.Length != 0)
                throw new InvalidOperationException("GameType 3 未中獎票不可出現三個相同金額。");
            return;
        }

        if (triples.Length != 1 || triples[0].Amount != expectedPrizeAmount)
        {
            throw new InvalidOperationException(
                "GameType 3 中獎票必須且只能有一組三個相同，且金額必須等於目標 Prize Tier。");
        }
    }
}
