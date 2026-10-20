using System.IO.Compression;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScratchGame.Engine;
using ScratchGame.Models;
using ScratchGame.Services;

namespace ScratchGame.Regression;

internal static class GameType4Regression
{
    public static void Run()
    {
        RequireDedicatedCiEnvironment();

        var root = Path.Combine(Path.GetTempPath(), "ScratchGameType4Regression", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            ValidateProgressiveMode(root);
            ValidateMultiSymbolMode(root);
            Console.WriteLine("PASS: GameType 4 loader / validator / generator / renderer contract");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void ValidateProgressiveMode(string root)
    {
        var packagePath = BuildPackage(
            Path.Combine(root, "progressive-source"),
            "GameType4-Progressive.scratchpack",
            new
            {
                mode = "singleSymbolProgressive",
                zoneCount = 9,
                targetSymbol = "★",
                minimumMatchCount = 3,
                useCustomDecoySymbols = true,
                decoySymbols = new[] { "○", "◆", "●" }
            },
            new[]
            {
                new { amount = 100L, count = 1L },
                new { amount = 500L, count = 1L },
                new { amount = 1000L, count = 1L }
            },
            issueSize: 4,
            zoneCount: 9,
            packageId: "a4b7ea2b-6231-4bb6-b79f-8469f76e99bf");

        var loaded = new ScratchPackV1Loader().LoadAndValidate(
            packagePath,
            Path.Combine(root, "progressive-load"));
        var ticket = loaded.Ticket;

        AssertEqual("4", ticket.GameType, "Type 4 progressive gameType");
        AssertEqual(GameType4Rules.SingleSymbolProgressive, ticket.SymbolCountMode, "Type 4 progressive mode");
        AssertEqual("★", ticket.TargetSymbol, "Type 4 target symbol");
        AssertEqual(3, ticket.MinimumMatchCount!.Value, "Type 4 minimumMatchCount");
        AssertEqual(9, ticket.ZoneCount!.Value, "Type 4 progressive zoneCount");

        foreach (var amount in new long[] { 0, 100, 500, 1000 })
        {
            for (var attempt = 0; attempt < 30; attempt++)
            {
                using var payload = JsonDocument.Parse(GamePayloadFactory.Create(ticket, amount));
                ValidatePayloadAndRenderModel(ticket, payload.RootElement, amount);
                var symbols = payload.RootElement.GetProperty("symbols").EnumerateArray()
                    .Select(item => item.GetString()!)
                    .ToArray();
                var targetCount = symbols.Count(symbol => symbol == "★");
                var expectedCount = amount switch
                {
                    100 => 3,
                    500 => 4,
                    1000 => 5,
                    _ => targetCount
                };
                if (amount == 0)
                    Assert(targetCount < 3, "progressive losing ticket stays below minimumMatchCount");
                else
                    AssertEqual(expectedCount, targetCount, "progressive target symbol count matches tier");
            }
        }

        var badDecoy = ticket with { DecoySymbols = new[] { "★" } };
        AssertThrows<InvalidDataException>(
            () => GameType4Rules.ValidateDefinition(badDecoy),
            "progressive decoySymbols must not contain targetSymbol");
    }

    private static void ValidateMultiSymbolMode(string root)
    {
        var packagePath = BuildPackage(
            Path.Combine(root, "multi-source"),
            "GameType4-Multi.scratchpack",
            new
            {
                mode = "multiSymbolFixedCount",
                zoneCount = 12,
                matchCount = 3,
                symbolPrizes = new[]
                {
                    new { symbol = "★", amount = 100L },
                    new { symbol = "○", amount = 300L },
                    new { symbol = "◆", amount = 700L }
                },
                allowMultipleWins = true,
                useCustomDecoySymbols = true,
                decoySymbols = new[] { "●", "▲", "■", "♥" }
            },
            new[]
            {
                new { amount = 100L, count = 1L },
                new { amount = 300L, count = 1L },
                new { amount = 400L, count = 1L },
                new { amount = 700L, count = 1L },
                new { amount = 800L, count = 1L },
                new { amount = 1000L, count = 1L },
                new { amount = 1100L, count = 1L }
            },
            issueSize: 8,
            zoneCount: 12,
            packageId: "e89e0b09-4316-4de0-9f7d-9f3b7b5b1040");

        var loaded = new ScratchPackV1Loader().LoadAndValidate(
            packagePath,
            Path.Combine(root, "multi-load"));
        var ticket = loaded.Ticket;

        AssertEqual(GameType4Rules.MultiSymbolFixedCount, ticket.SymbolCountMode, "Type 4 multi mode");
        AssertEqual(3, ticket.MatchCount!.Value, "Type 4 matchCount");
        AssertEqual(true, ticket.AllowMultipleWins!.Value, "Type 4 allowMultipleWins");
        AssertEqual(3, ticket.SymbolPrizes!.Count, "Type 4 symbol prize count");
        AssertSequenceEqual(
            new long[] { 100, 300, 400, 700, 800, 1000, 1100 },
            GameType4Rules.GetDerivablePrizeAmounts(ticket),
            "Type 4 derived multi-win prize amounts");

        foreach (var amount in new long[] { 0, 100, 300, 400, 700, 800, 1000, 1100 })
        {
            for (var attempt = 0; attempt < 30; attempt++)
            {
                using var payload = JsonDocument.Parse(GamePayloadFactory.Create(ticket, amount));
                ValidatePayloadAndRenderModel(ticket, payload.RootElement, amount);
                var symbols = payload.RootElement.GetProperty("symbols").EnumerateArray()
                    .Select(item => item.GetString()!)
                    .ToArray();
                foreach (var prizeSymbol in ticket.SymbolPrizes)
                {
                    var count = symbols.Count(symbol => symbol == prizeSymbol.Symbol);
                    Assert(count <= 3, "multi-symbol prize symbol never exceeds matchCount");
                }
            }
        }

        var singleWin = ticket with
        {
            AllowMultipleWins = false,
            Prizes = new ScratchPackPrize[]
            {
                new(100, 1),
                new(300, 1),
                new(700, 1)
            },
            IssueSize = 4
        };
        GameType4Rules.ValidateDefinition(singleWin);
        AssertSequenceEqual(
            new long[] { 100, 300, 700 },
            GameType4Rules.GetDerivablePrizeAmounts(singleWin),
            "Type 4 single-win derived prize amounts");
        foreach (var amount in new long[] { 0, 100, 300, 700 })
        {
            using var payload = JsonDocument.Parse(GamePayloadFactory.Create(singleWin, amount));
            ValidatePayloadAndRenderModel(singleWin, payload.RootElement, amount);
        }

        var missingDerivedTier = ticket with
        {
            Prizes = ticket.Prizes.Where(prize => prize.Amount != 400).ToArray()
        };
        AssertThrows<InvalidDataException>(
            () => GameType4Rules.ValidateDefinition(missingDerivedTier),
            "multi-symbol prizes must include every derivable total");

        var overlappingDecoy = ticket with { DecoySymbols = new[] { "★", "●" } };
        AssertThrows<InvalidDataException>(
            () => GameType4Rules.ValidateDefinition(overlappingDecoy),
            "multi-symbol decoySymbols must not overlap prize symbols");
    }

    private static void ValidatePayloadAndRenderModel(
        ScratchPackTicketDefinition ticket,
        JsonElement payload,
        long expectedPrize)
    {
        AssertEqual("4", payload.GetProperty("gameType").GetString(), "payload gameType");
        AssertEqual(ticket.SymbolCountMode, payload.GetProperty("mode").GetString(), "payload mode");
        AssertEqual(expectedPrize, payload.GetProperty("prizeAmount").GetInt64(), "payload prizeAmount");
        AssertEqual(expectedPrize, payload.GetProperty("actualPrizeAmount").GetInt64(), "payload actualPrizeAmount");
        AssertEqual(ticket.ZoneCount!.Value, payload.GetProperty("zoneCount").GetInt32(), "payload zoneCount");

        var symbols = payload.GetProperty("symbols").EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        AssertEqual(ticket.ZoneCount.Value, symbols.Length, "payload symbol count");
        AssertEqual(expectedPrize, GameType4Rules.CalculateActualPrize(ticket, symbols), "calculated prize amount");

        var cells = GameType4RenderModel.Build(ticket, payload);
        AssertEqual(ticket.Zones.Count, cells.Count, "Type 4 render cell count equals zones");
        for (var index = 0; index < cells.Count; index++)
        {
            AssertEqual(index, cells[index].ZoneIndex, $"Type 4 render cell {index} zone index");
            AssertEqual(ticket.Zones[index], cells[index].Zone, $"Type 4 render cell {index} zone");
            AssertEqual(symbols[index], cells[index].Symbol, $"Type 4 render cell {index} symbol");
        }
    }

    private static string BuildPackage<TGame, TPrize>(
        string source,
        string packageName,
        TGame game,
        TPrize[] prizes,
        int issueSize,
        int zoneCount,
        string packageId)
    {
        var assets = Path.Combine(source, "assets");
        Directory.CreateDirectory(assets);
        WritePng(Path.Combine(assets, "ticket.png"), 1080, 882);
        WritePng(Path.Combine(assets, "foil.png"), 32, 32);

        var manifest = new
        {
            formatVersion = "1.0",
            packageId,
            author = "ScratchGame Regression",
            minimumAppVersion = "0.5.7",
            ticketFile = "ticket.json"
        };
        File.WriteAllText(
            Path.Combine(source, "manifest.json"),
            JsonSerializer.Serialize(manifest));

        var zones = BuildZones(zoneCount);
        var ticket = new
        {
            name = "GameType 4 Regression",
            price = 100,
            canvas = 1,
            priceDisplay = 0,
            gameType = "4",
            issueSize,
            ticketsPerBook = issueSize,
            art = new { ticket = new { source = "package", @ref = "assets/ticket.png" } },
            serialDisplayArea = new { x = 365, y = 810, width = 350, height = 50 },
            scratch = new
            {
                foil = new { source = "package", @ref = "assets/foil.png" },
                zones
            },
            game,
            prizes
        };
        File.WriteAllText(
            Path.Combine(source, "ticket.json"),
            JsonSerializer.Serialize(ticket));

        var packagePath = Path.Combine(Path.GetDirectoryName(source)!, packageName);
        ZipFile.CreateFromDirectory(source, packagePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        return packagePath;
    }

    private static object[] BuildZones(int zoneCount)
    {
        var result = new List<object>();
        var columns = zoneCount <= 9 ? 3 : 4;
        const int width = 180;
        const int height = 100;
        const int startX = 100;
        const int startY = 160;
        const int xStep = 220;
        const int yStep = 135;

        for (var index = 0; index < zoneCount; index++)
        {
            var column = index % columns;
            var row = index / columns;
            result.Add(new
            {
                id = $"symbol-{index + 1:00}",
                x = startX + column * xStep,
                y = startY + row * yStep,
                width,
                height,
                shape = "roundedRectangle",
                cornerRadius = 12
            });
        }
        return result.ToArray();
    }

    private static void WritePng(string path, int width, int height)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void RequireDedicatedCiEnvironment()
    {
        var githubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
        var explicitGate = Environment.GetEnvironmentVariable("SCRATCHGAME_REGRESSION");
        if (!string.Equals(githubActions, "true", StringComparison.OrdinalIgnoreCase) || explicitGate != "1")
        {
            throw new InvalidOperationException(
                "GameType 4 regression may only use the dedicated GitHub Actions test environment. " +
                "Set SCRATCHGAME_REGRESSION=1 in CI.");
        }
    }

    private static void AssertThrows<TException>(Action action, string label)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}: {label}");
    }

    private static void AssertSequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string label)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"Assertion failed: {label}; expected=[{string.Join(",", expected)}]; actual=[{string.Join(",", actual)}]");
        }
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition)
            throw new InvalidOperationException("Assertion failed: " + label);
    }

    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Assertion failed: {label}; expected={expected}; actual={actual}");
    }
}
