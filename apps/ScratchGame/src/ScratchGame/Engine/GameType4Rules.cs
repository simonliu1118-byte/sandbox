using System.Text.Json;
using ScratchGame.Models;

namespace ScratchGame.Engine;

public static class GameType4Rules
{
    public const string SingleSymbolProgressive = "singleSymbolProgressive";
    public const string MultiSymbolFixedCount = "multiSymbolFixedCount";
    public const int MinZoneCount = 1;
    public const int MaxZoneCount = 25;
    public const int MaxPrizeSymbols = 10;

    private static readonly string[] DefaultDecoySymbols =
    [
        "●", "▲", "■", "◆", "♥", "♣", "♠", "☀",
        "☂", "☁", "☾", "✿", "✦", "✚", "✪", "⬟"
    ];

    public static ScratchPackTicketDefinition ParseDefinition(
        string name,
        long price,
        int canvas,
        bool priceDisplay,
        ScratchPackRect? priceDisplayArea,
        string gameType,
        long issueSize,
        long ticketsPerBook,
        ScratchPackResourceRef ticketArt,
        ScratchPackRect serialDisplayArea,
        ScratchPackResourceRef foil,
        IReadOnlyList<ScratchPackZone> zones,
        IReadOnlyList<ScratchPackPrize> prizes,
        string rawJson,
        JsonElement game)
    {
        EnsureObject(game, "game");
        var mode = RequiredString(game, "mode");
        if (mode is not SingleSymbolProgressive and not MultiSymbolFixedCount)
            throw new InvalidDataException($"GameType 4 不支援的 mode：{mode}");

        var zoneCount = RequiredInt32(game, "zoneCount");
        var useCustomDecoySymbols = game.TryGetProperty("useCustomDecoySymbols", out var customElement)
            ? RequiredBoolean(customElement, "game.useCustomDecoySymbols")
            : false;
        IReadOnlyList<string>? decoySymbols = null;
        if (game.TryGetProperty("decoySymbols", out var decoyElement))
            decoySymbols = ParseStringArray(decoyElement, "game.decoySymbols");

        ScratchPackTicketDefinition ticket;
        if (mode == SingleSymbolProgressive)
        {
            EnsureOnlyProperties(game, "game",
                "mode", "zoneCount", "targetSymbol", "minimumMatchCount",
                "useCustomDecoySymbols", "decoySymbols");

            ticket = new ScratchPackTicketDefinition(
                name,
                price,
                canvas,
                priceDisplay,
                priceDisplayArea,
                gameType,
                issueSize,
                ticketsPerBook,
                ticketArt,
                serialDisplayArea,
                foil,
                zones,
                GridSize: 0,
                AllowNearMiss: false,
                prizes,
                rawJson,
                ZoneCount: zoneCount,
                SymbolCountMode: mode,
                TargetSymbol: RequiredString(game, "targetSymbol").Trim(),
                MinimumMatchCount: RequiredInt32(game, "minimumMatchCount"),
                UseCustomDecoySymbols: useCustomDecoySymbols,
                DecoySymbols: decoySymbols);
        }
        else
        {
            EnsureOnlyProperties(game, "game",
                "mode", "zoneCount", "matchCount", "symbolPrizes", "allowMultipleWins",
                "useCustomDecoySymbols", "decoySymbols");

            ticket = new ScratchPackTicketDefinition(
                name,
                price,
                canvas,
                priceDisplay,
                priceDisplayArea,
                gameType,
                issueSize,
                ticketsPerBook,
                ticketArt,
                serialDisplayArea,
                foil,
                zones,
                GridSize: 0,
                AllowNearMiss: false,
                prizes,
                rawJson,
                ZoneCount: zoneCount,
                SymbolCountMode: mode,
                MatchCount: RequiredInt32(game, "matchCount"),
                SymbolPrizes: ParseSymbolPrizes(RequiredProperty(game, "symbolPrizes")),
                AllowMultipleWins: RequiredBoolean(
                    RequiredProperty(game, "allowMultipleWins"),
                    "game.allowMultipleWins"),
                UseCustomDecoySymbols: useCustomDecoySymbols,
                DecoySymbols: decoySymbols);
        }

        ValidateDefinition(ticket);
        return ticket;
    }

    public static void ValidateDefinition(ScratchPackTicketDefinition ticket)
    {
        if (ticket.GameType != "4")
            throw new InvalidDataException("GameType4Rules 只能驗證 GameType 4。");

        var zoneCount = ticket.ZoneCount
            ?? throw new InvalidDataException("GameType 4 缺少 zoneCount。");
        if (zoneCount is < MinZoneCount or > MaxZoneCount)
        {
            throw new InvalidDataException(
                $"GameType 4 zoneCount 只允許 {MinZoneCount}～{MaxZoneCount}。");
        }

        ValidateZones(ticket.Zones, zoneCount);
        ValidateDecoyConfiguration(ticket);

        switch (ticket.SymbolCountMode)
        {
            case SingleSymbolProgressive:
                ValidateSingleSymbolProgressive(ticket);
                break;
            case MultiSymbolFixedCount:
                ValidateMultiSymbolFixedCount(ticket);
                break;
            default:
                throw new InvalidDataException("GameType 4 缺少或含無效 mode。");
        }
    }

    public static object CreatePayload(ScratchPackTicketDefinition ticket, long prizeAmount)
    {
        ValidateDefinition(ticket);
        ValidateRequestedPrize(ticket, prizeAmount);

        var symbols = ticket.SymbolCountMode switch
        {
            SingleSymbolProgressive => CreateSingleSymbolProgressiveSymbols(ticket, prizeAmount),
            MultiSymbolFixedCount => CreateMultiSymbolFixedCountSymbols(ticket, prizeAmount),
            _ => throw new InvalidOperationException("GameType 4 mode 無效。")
        };

        var actualPrizeAmount = CalculateActualPrize(ticket, symbols);
        if (actualPrizeAmount != prizeAmount)
        {
            throw new InvalidOperationException(
                $"GameType 4 產生結果 ${actualPrizeAmount:N0} 與目標 Prize Tier ${prizeAmount:N0} 不一致。");
        }

        return new
        {
            ruleId = "4",
            gameType = "4",
            mode = ticket.SymbolCountMode,
            prizeAmount,
            actualPrizeAmount,
            zoneCount = ticket.ZoneCount!.Value,
            symbols
        };
    }

    public static long CalculateActualPrize(
        ScratchPackTicketDefinition ticket,
        IReadOnlyList<string> symbols)
    {
        if (symbols.Count != ticket.ZoneCount)
            throw new InvalidDataException("GameType 4 symbol 數量與 zoneCount 不一致。");

        var counts = symbols
            .GroupBy(symbol => symbol, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        if (ticket.SymbolCountMode == SingleSymbolProgressive)
        {
            var target = ticket.TargetSymbol!;
            var targetCount = counts.GetValueOrDefault(target);
            var minimum = ticket.MinimumMatchCount!.Value;
            if (targetCount < minimum)
                return 0;

            var index = targetCount - minimum;
            var orderedPrizes = ticket.Prizes.OrderBy(prize => prize.Amount).ToArray();
            if (index < 0 || index >= orderedPrizes.Length)
                throw new InvalidDataException("GameType 4 progressive target symbol 次數超出合法 Prize Tier。");
            return orderedPrizes[index].Amount;
        }

        if (ticket.SymbolCountMode != MultiSymbolFixedCount)
            throw new InvalidDataException("GameType 4 mode 無效。");

        var matchCount = ticket.MatchCount!.Value;
        long total = 0;
        var winCount = 0;
        foreach (var symbolPrize in ticket.SymbolPrizes!)
        {
            var count = counts.GetValueOrDefault(symbolPrize.Symbol);
            if (count > matchCount)
            {
                throw new InvalidDataException(
                    $"GameType 4 有獎符號 {symbolPrize.Symbol} 出現 {count} 次，超過 matchCount={matchCount}。");
            }
            if (count != matchCount)
                continue;

            checked { total += symbolPrize.Amount; }
            winCount++;
        }

        if (ticket.AllowMultipleWins != true && winCount > 1)
            throw new InvalidDataException("GameType 4 allowMultipleWins=false 時不可同時成立多種有獎符號。");
        return total;
    }

    public static IReadOnlyList<long> GetDerivablePrizeAmounts(ScratchPackTicketDefinition ticket)
    {
        if (ticket.SymbolCountMode == SingleSymbolProgressive)
            return ticket.Prizes.Select(prize => prize.Amount).OrderBy(amount => amount).ToArray();

        if (ticket.SymbolCountMode != MultiSymbolFixedCount || ticket.SymbolPrizes is null || ticket.MatchCount is null)
            return Array.Empty<long>();

        if (ticket.AllowMultipleWins != true)
        {
            return ticket.SymbolPrizes
                .Select(item => item.Amount)
                .Distinct()
                .OrderBy(amount => amount)
                .ToArray();
        }

        var symbolPrizes = ticket.SymbolPrizes.ToArray();
        var maxWinningSymbols = ticket.ZoneCount!.Value / ticket.MatchCount.Value;
        var amounts = new HashSet<long>();
        var limit = 1 << symbolPrizes.Length;
        for (var mask = 1; mask < limit; mask++)
        {
            var selectedCount = 0;
            long total = 0;
            for (var index = 0; index < symbolPrizes.Length; index++)
            {
                if ((mask & (1 << index)) == 0)
                    continue;
                selectedCount++;
                checked { total += symbolPrizes[index].Amount; }
            }

            if (selectedCount <= maxWinningSymbols)
                amounts.Add(total);
        }

        return amounts.OrderBy(amount => amount).ToArray();
    }

    private static void ValidateSingleSymbolProgressive(ScratchPackTicketDefinition ticket)
    {
        ValidateSymbol(ticket.TargetSymbol, "game.targetSymbol");
        var minimum = ticket.MinimumMatchCount
            ?? throw new InvalidDataException("GameType 4 progressive 缺少 minimumMatchCount。");
        if (minimum <= 0)
            throw new InvalidDataException("GameType 4 minimumMatchCount 必須 > 0。");
        if (ticket.Prizes.Count == 0)
            throw new InvalidDataException("GameType 4 progressive 至少需要一個 Prize Tier。");
        if (minimum + ticket.Prizes.Count - 1 > ticket.ZoneCount)
        {
            throw new InvalidDataException(
                "GameType 4 progressive 的 minimumMatchCount + Prize Tier 數量超過 zoneCount 可表示範圍。");
        }

        var amounts = ticket.Prizes.Select(prize => prize.Amount).ToArray();
        if (!amounts.SequenceEqual(amounts.OrderBy(amount => amount)))
            throw new InvalidDataException("GameType 4 progressive Prize Tier 必須依金額嚴格遞增。");

        if (ticket.DecoySymbols?.Contains(ticket.TargetSymbol!, StringComparer.Ordinal) == true)
            throw new InvalidDataException("GameType 4 decoySymbols 不得包含 targetSymbol。");

        if (ticket.MatchCount is not null || ticket.SymbolPrizes is not null || ticket.AllowMultipleWins is not null)
            throw new InvalidDataException("GameType 4 progressive 不接受 Mode B 專用欄位。");
    }

    private static void ValidateMultiSymbolFixedCount(ScratchPackTicketDefinition ticket)
    {
        var matchCount = ticket.MatchCount
            ?? throw new InvalidDataException("GameType 4 multi-symbol 缺少 matchCount。");
        if (matchCount <= 0 || matchCount > ticket.ZoneCount)
            throw new InvalidDataException("GameType 4 matchCount 必須 > 0 且不可超過 zoneCount。");

        var symbolPrizes = ticket.SymbolPrizes?.ToArray()
            ?? throw new InvalidDataException("GameType 4 multi-symbol 缺少 symbolPrizes。");
        if (symbolPrizes.Length == 0 || symbolPrizes.Length > MaxPrizeSymbols)
        {
            throw new InvalidDataException(
                $"GameType 4 symbolPrizes 必須有 1～{MaxPrizeSymbols} 個有獎符號。");
        }
        if (ticket.AllowMultipleWins is null)
            throw new InvalidDataException("GameType 4 multi-symbol 缺少 allowMultipleWins。");

        var symbols = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in symbolPrizes)
        {
            ValidateSymbol(item.Symbol, "game.symbolPrizes[].symbol");
            if (item.Amount <= 0)
                throw new InvalidDataException("GameType 4 symbolPrizes[].amount 必須 > 0。");
            if (!symbols.Add(item.Symbol))
                throw new InvalidDataException($"GameType 4 有獎符號重複：{item.Symbol}");
        }

        if (ticket.DecoySymbols?.Any(symbols.Contains) == true)
            throw new InvalidDataException("GameType 4 decoySymbols 不得與任何有獎符號重複。");

        var derived = GetDerivablePrizeAmounts(ticket);
        var defined = ticket.Prizes.Select(prize => prize.Amount).OrderBy(amount => amount).ToArray();
        if (!derived.SequenceEqual(defined))
        {
            throw new InvalidDataException(
                "GameType 4 multi-symbol 的 prizes 必須精確等於依 symbolPrizes、matchCount、allowMultipleWins 與 zoneCount 推導出的所有合法總獎金；未發行組合請保留 count=0。");
        }

        if (ticket.TargetSymbol is not null || ticket.MinimumMatchCount is not null)
            throw new InvalidDataException("GameType 4 multi-symbol 不接受 Mode A 專用欄位。");
    }

    private static void ValidateZones(IReadOnlyList<ScratchPackZone> zones, int zoneCount)
    {
        if (zones.Count != zoneCount)
            throw new InvalidDataException($"GameType 4 必須精確包含 {zoneCount} 個 scratch zones。");
        if (zones.Select(zone => zone.Id).Distinct(StringComparer.Ordinal).Count() != zones.Count)
            throw new InvalidDataException("GameType 4 scratch zone id 不得重複。");

        var first = zones[0];
        if (zones.Any(zone =>
                zone.Width != first.Width || zone.Height != first.Height || zone.Shape != first.Shape ||
                zone.CornerRadius != first.CornerRadius))
        {
            throw new InvalidDataException(
                "GameType 4 所有 scratch zones 的尺寸、shape 與 cornerRadius 必須一致。");
        }
    }

    private static void ValidateDecoyConfiguration(ScratchPackTicketDefinition ticket)
    {
        var custom = ticket.UseCustomDecoySymbols ?? false;
        var decoys = ticket.DecoySymbols?.ToArray() ?? Array.Empty<string>();
        if (!custom && decoys.Length > 0)
            throw new InvalidDataException("useCustomDecoySymbols=false 時不得提供 decoySymbols。");
        if (custom && decoys.Length == 0)
            throw new InvalidDataException("useCustomDecoySymbols=true 時必須提供至少一個 decoySymbols。");

        var distinct = new HashSet<string>(StringComparer.Ordinal);
        foreach (var symbol in decoys)
        {
            ValidateSymbol(symbol, "game.decoySymbols[]");
            if (!distinct.Add(symbol))
                throw new InvalidDataException($"GameType 4 decoySymbols 重複：{symbol}");
        }
    }

    private static void ValidateRequestedPrize(ScratchPackTicketDefinition ticket, long prizeAmount)
    {
        var winningCount = ticket.Prizes.Sum(prize => prize.Count);
        var losingCount = ticket.IssueSize - winningCount;
        if (prizeAmount == 0)
        {
            if (losingCount <= 0)
                throw new InvalidOperationException("GameType 4 此 Pack 沒有未中獎票，不能生成 prizeAmount=0。");
            return;
        }

        if (!ticket.Prizes.Any(prize => prize.Amount == prizeAmount && prize.Count > 0))
            throw new InvalidOperationException($"GameType 4 沒有可發行的 Prize Tier ${prizeAmount:N0}。");
    }

    private static string[] CreateSingleSymbolProgressiveSymbols(
        ScratchPackTicketDefinition ticket,
        long prizeAmount)
    {
        var target = ticket.TargetSymbol!;
        var minimum = ticket.MinimumMatchCount!.Value;
        int targetCount;
        if (prizeAmount == 0)
        {
            targetCount = minimum == 1 ? 0 : Random.Shared.Next(minimum);
        }
        else
        {
            var ordered = ticket.Prizes.OrderBy(prize => prize.Amount).ToArray();
            var index = Array.FindIndex(ordered, prize => prize.Amount == prizeAmount);
            if (index < 0)
                throw new InvalidOperationException("GameType 4 找不到 progressive Prize Tier。");
            targetCount = minimum + index;
        }

        var result = Enumerable.Repeat(target, targetCount).ToList();
        var decoys = GetDecoySymbols(ticket);
        while (result.Count < ticket.ZoneCount)
            result.Add(decoys[Random.Shared.Next(decoys.Count)]);
        return Shuffle(result);
    }

    private static string[] CreateMultiSymbolFixedCountSymbols(
        ScratchPackTicketDefinition ticket,
        long prizeAmount)
    {
        var symbolPrizes = ticket.SymbolPrizes!.ToArray();
        var matchCount = ticket.MatchCount!.Value;
        var winningSymbols = ResolveWinningSymbols(ticket, prizeAmount);
        var result = new List<string>(ticket.ZoneCount!.Value);

        foreach (var symbol in winningSymbols)
            result.AddRange(Enumerable.Repeat(symbol, matchCount));

        var remaining = ticket.ZoneCount.Value - result.Count;
        var nonWinners = symbolPrizes
            .Select(item => item.Symbol)
            .Where(symbol => !winningSymbols.Contains(symbol, StringComparer.Ordinal))
            .OrderBy(_ => Random.Shared.Next())
            .ToArray();

        foreach (var symbol in nonWinners)
        {
            if (remaining <= 0)
                break;
            var maxCount = Math.Min(matchCount - 1, remaining);
            if (maxCount <= 0)
                continue;
            var count = Random.Shared.Next(maxCount + 1);
            if (count == 0)
                continue;
            result.AddRange(Enumerable.Repeat(symbol, count));
            remaining -= count;
        }

        var decoys = GetDecoySymbols(ticket);
        while (remaining-- > 0)
            result.Add(decoys[Random.Shared.Next(decoys.Count)]);

        return Shuffle(result);
    }

    private static string[] ResolveWinningSymbols(ScratchPackTicketDefinition ticket, long prizeAmount)
    {
        if (prizeAmount == 0)
            return Array.Empty<string>();

        var symbolPrizes = ticket.SymbolPrizes!.ToArray();
        if (ticket.AllowMultipleWins != true)
        {
            var candidates = symbolPrizes
                .Where(item => item.Amount == prizeAmount)
                .Select(item => item.Symbol)
                .ToArray();
            if (candidates.Length == 0)
                throw new InvalidOperationException("GameType 4 找不到對應單一中獎符號。");
            return [candidates[Random.Shared.Next(candidates.Length)]];
        }

        var matchCount = ticket.MatchCount!.Value;
        var maxWinningSymbols = ticket.ZoneCount!.Value / matchCount;
        var candidatesByMask = new List<string[]>();
        var limit = 1 << symbolPrizes.Length;
        for (var mask = 1; mask < limit; mask++)
        {
            var selected = new List<string>();
            long total = 0;
            for (var index = 0; index < symbolPrizes.Length; index++)
            {
                if ((mask & (1 << index)) == 0)
                    continue;
                selected.Add(symbolPrizes[index].Symbol);
                checked { total += symbolPrizes[index].Amount; }
            }

            if (selected.Count <= maxWinningSymbols && total == prizeAmount)
                candidatesByMask.Add(selected.ToArray());
        }

        if (candidatesByMask.Count == 0)
            throw new InvalidOperationException("GameType 4 找不到對應多重中獎符號組合。");
        return candidatesByMask[Random.Shared.Next(candidatesByMask.Count)];
    }

    private static IReadOnlyList<string> GetDecoySymbols(ScratchPackTicketDefinition ticket)
    {
        if (ticket.UseCustomDecoySymbols == true)
            return ticket.DecoySymbols!;

        var excluded = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(ticket.TargetSymbol))
            excluded.Add(ticket.TargetSymbol);
        if (ticket.SymbolPrizes is not null)
        {
            foreach (var item in ticket.SymbolPrizes)
                excluded.Add(item.Symbol);
        }

        var result = DefaultDecoySymbols.Where(symbol => !excluded.Contains(symbol)).ToArray();
        if (result.Length == 0)
            throw new InvalidOperationException("GameType 4 找不到可用的主程式干擾符號。");
        return result;
    }

    private static string[] Shuffle(IReadOnlyList<string> source)
        => source.OrderBy(_ => Random.Shared.Next()).ToArray();

    private static IReadOnlyList<ScratchPackSymbolPrize> ParseSymbolPrizes(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("game.symbolPrizes 必須是 array。");

        var result = new List<ScratchPackSymbolPrize>();
        foreach (var item in element.EnumerateArray())
        {
            EnsureObject(item, "game.symbolPrizes[]");
            EnsureOnlyProperties(item, "game.symbolPrizes[]", "symbol", "amount");
            result.Add(new ScratchPackSymbolPrize(
                RequiredString(item, "symbol").Trim(),
                RequiredInt64(item, "amount")));
        }
        return result;
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement element, string label)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"{label} 必須是 array。");

        var result = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"{label} 必須全部是字串。");
            var value = (item.GetString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException($"{label} 不得包含空白符號。");
            result.Add(value);
        }
        return result;
    }

    private static void ValidateSymbol(string? symbol, string label)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new InvalidDataException($"{label} 不得空白。");
        if (symbol != symbol.Trim())
            throw new InvalidDataException($"{label} 前後不得有空白。");
        if (symbol.Length > 8)
            throw new InvalidDataException($"{label} 最長 8 個 UTF-16 字元。");
    }

    private static void EnsureObject(JsonElement element, string label)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{label} 必須是 object。");
    }

    private static void EnsureOnlyProperties(JsonElement element, string label, params string[] allowed)
    {
        var set = allowed.ToHashSet(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!set.Contains(property.Name))
                throw new InvalidDataException($"{label} 含未定義欄位：{property.Name}");
        }
    }

    private static JsonElement RequiredProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
            throw new InvalidDataException($"缺少必要欄位：{name}");
        return property;
    }

    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"缺少或無效的字串欄位：{name}");
        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"欄位不可空白：{name}");
        return value;
    }

    private static int RequiredInt32(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || !property.TryGetInt32(out var value))
            throw new InvalidDataException($"缺少或無效的整數欄位：{name}");
        return value;
    }

    private static long RequiredInt64(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || !property.TryGetInt64(out var value))
            throw new InvalidDataException($"缺少或無效的整數欄位：{name}");
        return value;
    }

    private static bool RequiredBoolean(JsonElement element, string name)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException($"缺少或無效的布林欄位：{name}")
        };
    }
}
