using System.IO.Compression;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScratchGame.Engine;
using ScratchGame.Models;
using ScratchGame.Services;

namespace ScratchGame.Regression;

internal static class GameType3Regression
{
    public static void Run()
    {
        RequireDedicatedCiEnvironment();

        var root = Path.Combine(Path.GetTempPath(), "ScratchGameType3Regression", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var packagePath = BuildPackage(root);
            var loaded = new ScratchPackV1Loader().LoadAndValidate(
                packagePath,
                Path.Combine(root, "normal-load"));

            AssertEqual("3", loaded.Ticket.GameType, "Type 3 gameType");
            AssertEqual(9, loaded.Ticket.ZoneCount!.Value, "Type 3 zoneCount");
            AssertEqual(true, loaded.Ticket.UseCustomDecoyAmounts!.Value, "Type 3 custom decoy mode");
            AssertEqual(GameType3Rules.DefaultNearMissPairProbability,
                loaded.Ticket.NearMissPairProbability!.Value,
                "Type 3 default nearMissPairProbability");
            AssertEqual(GameType3Rules.DefaultNearMissPairCount,
                loaded.Ticket.NearMissPairCount!.Value,
                "Type 3 default nearMissPairCount");
            AssertEqual(6, loaded.Ticket.DecoyAmounts!.Count, "Type 3 decoy amount count");

            var forcedNearMiss = loaded.Ticket with
            {
                NearMissPairProbability = 100,
                NearMissPairCount = 2
            };
            GameType3Rules.ValidateDefinition(forcedNearMiss);

            foreach (var amount in new long[] { 0, 100, 500, 1_000 })
            {
                for (var attempt = 0; attempt < 30; attempt++)
                    ValidatePayloadAndRenderModel(forcedNearMiss, amount, minimumNearMissPairs: 2);
            }

            var overlappingDecoy = loaded.Ticket with
            {
                DecoyAmounts = new long[] { 50, 100, 750 }
            };
            AssertThrows<InvalidDataException>(
                () => GameType3Rules.ValidateDefinition(overlappingDecoy),
                "decoyAmounts must not overlap Prize Tier amounts");

            var impossibleNearMiss = loaded.Ticket with
            {
                ZoneCount = 4,
                Zones = loaded.Ticket.Zones.Take(4).ToArray(),
                NearMissPairProbability = 100,
                NearMissPairCount = 1
            };
            AssertThrows<InvalidDataException>(
                () => GameType3Rules.ValidateDefinition(impossibleNearMiss),
                "winning Type 3 ticket with one filler slot cannot guarantee a Near Miss pair");

            var insufficientAmounts = loaded.Ticket with
            {
                UseCustomDecoyAmounts = false,
                DecoyAmounts = null,
                NearMissPairProbability = 0,
                NearMissPairCount = 1
            };
            AssertThrows<InvalidDataException>(
                () => GameType3Rules.ValidateDefinition(insufficientAmounts),
                "available official prize amounts must be sufficient to fill every Type 3 outcome");

            Console.WriteLine("PASS: GameType 3 loader / validator / generator / renderer contract");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string BuildPackage(string root)
    {
        var source = Path.Combine(root, "source");
        var assets = Path.Combine(source, "assets");
        Directory.CreateDirectory(assets);
        WritePng(Path.Combine(assets, "ticket.png"), 1080, 882);
        WritePng(Path.Combine(assets, "foil.png"), 32, 32);

        File.WriteAllText(Path.Combine(source, "manifest.json"), """
            {
              "formatVersion": "1.0",
              "packageId": "ba6eb089-3d4c-4f95-9014-2d67e6b2e503",
              "author": "ScratchGame Regression",
              "minimumAppVersion": "0.5.6",
              "ticketFile": "ticket.json"
            }
            """);

        File.WriteAllText(Path.Combine(source, "ticket.json"), """
            {
              "name": "GameType 3 Regression",
              "price": 100,
              "canvas": 1,
              "priceDisplay": 0,
              "gameType": "3",
              "issueSize": 4,
              "ticketsPerBook": 4,
              "art": {
                "ticket": { "source": "package", "ref": "assets/ticket.png" }
              },
              "serialDisplayArea": { "x": 365, "y": 810, "width": 350, "height": 50 },
              "scratch": {
                "foil": { "source": "package", "ref": "assets/foil.png" },
                "zones": [
                  { "id": "amount-01", "x": 100, "y": 160, "width": 180, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 },
                  { "id": "amount-02", "x": 310, "y": 160, "width": 180, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 },
                  { "id": "amount-03", "x": 520, "y": 160, "width": 180, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 },
                  { "id": "amount-04", "x": 100, "y": 300, "width": 180, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 },
                  { "id": "amount-05", "x": 310, "y": 300, "width": 180, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 },
                  { "id": "amount-06", "x": 520, "y": 300, "width": 180, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 },
                  { "id": "amount-07", "x": 100, "y": 440, "width": 180, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 },
                  { "id": "amount-08", "x": 310, "y": 440, "width": 180, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 },
                  { "id": "amount-09", "x": 520, "y": 440, "width": 180, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 }
                ]
              },
              "game": {
                "zoneCount": 9,
                "useCustomDecoyAmounts": true,
                "decoyAmounts": [50, 200, 750, 2000, 5000, 20000]
              },
              "prizes": [
                { "amount": 100, "count": 1 },
                { "amount": 500, "count": 1 },
                { "amount": 1000, "count": 1 }
              ]
            }
            """);

        var packagePath = Path.Combine(root, "GameType3-Runtime.scratchpack");
        ZipFile.CreateFromDirectory(source, packagePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        return packagePath;
    }

    private static void ValidatePayloadAndRenderModel(
        ScratchPackTicketDefinition ticket,
        long expectedPrize,
        int minimumNearMissPairs)
    {
        using var document = JsonDocument.Parse(GamePayloadFactory.Create(ticket, expectedPrize));
        var root = document.RootElement;
        AssertEqual("3", root.GetProperty("gameType").GetString(), "payload gameType");
        AssertEqual(expectedPrize, root.GetProperty("prizeAmount").GetInt64(), "payload prizeAmount");
        AssertEqual(expectedPrize, root.GetProperty("actualPrizeAmount").GetInt64(), "payload actualPrizeAmount");
        AssertEqual(ticket.ZoneCount!.Value, root.GetProperty("zoneCount").GetInt32(), "payload zoneCount");

        var amounts = root.GetProperty("amounts").EnumerateArray().Select(item => item.GetInt64()).ToArray();
        AssertEqual(ticket.ZoneCount.Value, amounts.Length, "payload amount count");

        var groups = amounts.GroupBy(amount => amount).ToDictionary(group => group.Key, group => group.Count());
        Assert(groups.Values.All(count => count <= 3), "no Type 3 amount may occur more than three times");

        var triples = groups.Where(pair => pair.Value == 3).ToArray();
        if (expectedPrize == 0)
        {
            AssertEqual(0, triples.Length, "losing Type 3 ticket has no triple");
        }
        else
        {
            AssertEqual(1, triples.Length, "winning Type 3 ticket has exactly one triple");
            AssertEqual(expectedPrize, triples[0].Key, "winning Type 3 triple matches Prize Tier");
        }

        var pairCount = groups.Count(pair => pair.Key != expectedPrize && pair.Value == 2);
        Assert(pairCount >= minimumNearMissPairs,
            $"near miss mode must guarantee at least {minimumNearMissPairs} non-winning pairs");

        var cells = GameType3RenderModel.Build(ticket, root);
        AssertEqual(ticket.Zones.Count, cells.Count, "Type 3 render cell count equals zones");
        for (var index = 0; index < cells.Count; index++)
        {
            AssertEqual(index, cells[index].ZoneIndex, $"Type 3 render cell {index} zone index");
            AssertEqual(ticket.Zones[index], cells[index].Zone, $"Type 3 render cell {index} zone");
            AssertEqual(amounts[index], cells[index].Amount, $"Type 3 render cell {index} amount");
        }
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
                "GameType 3 regression may only use the dedicated GitHub Actions test environment. " +
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
