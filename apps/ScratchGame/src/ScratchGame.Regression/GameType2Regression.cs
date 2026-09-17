using System.IO.Compression;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScratchGame.Engine;
using ScratchGame.Models;
using ScratchGame.Services;

namespace ScratchGame.Regression;

internal static class GameType2Regression
{
    public static void Run()
    {
        RequireDedicatedCiEnvironment();

        var root = Path.Combine(Path.GetTempPath(), "ScratchGameType2Regression", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var packagePath = BuildPackage(root);
            var loader = new ScratchPackV1Loader();

            AssertThrows<InvalidDataException>(
                () => loader.LoadAndValidate(packagePath, Path.Combine(root, "blocked-load")),
                "Type 2 must remain blocked from normal install until its Renderer is complete");

            var loaded = loader.LoadAndValidate(
                packagePath,
                Path.Combine(root, "core-load"),
                allowUnrenderedGameTypes: true);

            AssertEqual("2", loaded.Ticket.GameType, "Type 2 gameType");
            AssertEqual(2, loaded.Ticket.WinningNumberCount!.Value, "winningNumberCount");
            AssertEqual(4, loaded.Ticket.PlayNumberCount!.Value, "playNumberCount");
            AssertEqual("play", loaded.Ticket.PayoutSource, "payoutSource");
            AssertEqual(false, loaded.Ticket.AllowPrizeAmountRepeat!.Value, "allowPrizeAmountRepeat");
            AssertEqual(6, loaded.Ticket.Zones.Count, "Type 2 zone count");

            foreach (var amount in new long[] { 0, 100, 300, 500 })
            {
                for (var attempt = 0; attempt < 20; attempt++)
                    ValidatePayload(loaded.Ticket, amount);
            }

            var winningPayout = loaded.Ticket with { PayoutSource = "winning" };
            GameType2Rules.ValidateDefinition(winningPayout);
            foreach (var amount in new long[] { 0, 100, 300, 500 })
            {
                for (var attempt = 0; attempt < 10; attempt++)
                    ValidatePayload(winningPayout, amount);
            }

            var duplicateDisplayAmounts = loaded.Ticket with
            {
                DisplayPrizeAmounts = new long[] { 100, 100, 300, 500 }
            };
            AssertThrows<InvalidDataException>(
                () => GameType2Rules.ValidateDefinition(duplicateDisplayAmounts),
                "displayPrizeAmounts must not contain duplicates");

            var impossiblePrize = loaded.Ticket with
            {
                Prizes = new[] { new ScratchPackPrize(175, 1) }
            };
            AssertThrows<InvalidDataException>(
                () => GameType2Rules.ValidateDefinition(impossiblePrize),
                "every positive Prize Tier must be exactly generatable");

            Console.WriteLine("PASS: GameType 2 loader / validator / payload generator core contract");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void RequireDedicatedCiEnvironment()
    {
        var githubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
        var explicitGate = Environment.GetEnvironmentVariable("SCRATCHGAME_REGRESSION");
        if (!string.Equals(githubActions, "true", StringComparison.OrdinalIgnoreCase) || explicitGate != "1")
        {
            throw new InvalidOperationException(
                "GameType 2 regression may only use the dedicated GitHub Actions test environment. " +
                "Set SCRATCHGAME_REGRESSION=1 in CI.");
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
              "packageId": "6fb7f51d-f1a8-4b4d-9b67-6d1200dc0d02",
              "author": "ScratchGame Regression",
              "minimumAppVersion": "0.5.4",
              "ticketFile": "ticket.json"
            }
            """);

        File.WriteAllText(Path.Combine(source, "ticket.json"), """
            {
              "name": "GameType 2 Regression",
              "price": 100,
              "canvas": 1,
              "priceDisplay": 0,
              "gameType": "2",
              "issueSize": 4,
              "ticketsPerBook": 4,
              "art": {
                "ticket": { "source": "package", "ref": "assets/ticket.png" }
              },
              "serialDisplayArea": { "x": 365, "y": 810, "width": 350, "height": 50 },
              "scratch": {
                "foil": { "source": "package", "ref": "assets/foil.png" },
                "zones": [
                  { "id": "winning-01", "x": 100, "y": 160, "width": 120, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 },
                  { "id": "winning-02", "x": 250, "y": 160, "width": 120, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 },
                  { "id": "play-01", "x": 100, "y": 340, "width": 120, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 },
                  { "id": "play-02", "x": 250, "y": 340, "width": 120, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 },
                  { "id": "play-03", "x": 400, "y": 340, "width": 120, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 },
                  { "id": "play-04", "x": 550, "y": 340, "width": 120, "height": 100, "shape": "roundedRectangle", "cornerRadius": 12 }
                ]
              },
              "game": {
                "winningNumberCount": 2,
                "playNumberCount": 4,
                "numberMin": 1,
                "numberMax": 20,
                "payoutSource": "play",
                "displayPrizeAmounts": [100, 200, 300, 500],
                "allowPrizeAmountRepeat": false
              },
              "prizes": [
                { "amount": 100, "count": 1 },
                { "amount": 300, "count": 1 },
                { "amount": 500, "count": 1 }
              ]
            }
            """);

        var packagePath = Path.Combine(root, "GameType2-Core.scratchpack");
        ZipFile.CreateFromDirectory(source, packagePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        return packagePath;
    }

    private static void ValidatePayload(ScratchPackTicketDefinition ticket, long expectedPrize)
    {
        using var document = JsonDocument.Parse(GamePayloadFactory.Create(ticket, expectedPrize));
        var root = document.RootElement;
        var payoutSource = ticket.PayoutSource!;

        AssertEqual("2", root.GetProperty("gameType").GetString(), "payload gameType");
        AssertEqual(expectedPrize, root.GetProperty("prizeAmount").GetInt64(), "payload prizeAmount");
        AssertEqual(expectedPrize, root.GetProperty("actualPrizeAmount").GetInt64(), "payload actualPrizeAmount");
        AssertEqual(payoutSource, root.GetProperty("payoutSource").GetString(), "payload payoutSource");

        var winning = root.GetProperty("winningNumbers").EnumerateArray().Select(item => item.GetInt64()).ToArray();
        var play = root.GetProperty("playNumbers").EnumerateArray().Select(item => item.GetInt64()).ToArray();
        var winningAmounts = root.GetProperty("winningPrizeAmounts").EnumerateArray().Select(item => item.GetInt64()).ToArray();
        var playAmounts = root.GetProperty("playPrizeAmounts").EnumerateArray().Select(item => item.GetInt64()).ToArray();
        var matchedWinningIndexes = root.GetProperty("matchedWinningIndexes").EnumerateArray().Select(item => item.GetInt32()).ToArray();
        var matchedPlayIndexes = root.GetProperty("matchedPlayIndexes").EnumerateArray().Select(item => item.GetInt32()).ToArray();

        AssertEqual(2, winning.Length, "winning number payload count");
        AssertEqual(4, play.Length, "play number payload count");
        AssertEqual(winning.Length, winning.Distinct().Count(), "winning numbers unique");
        AssertEqual(play.Length, play.Distinct().Count(), "play numbers unique");

        var winningSet = winning.ToHashSet();
        var playSet = play.ToHashSet();
        var actualMatchedWinningIndexes = winning
            .Select((number, index) => (number, index))
            .Where(item => playSet.Contains(item.number))
            .Select(item => item.index)
            .ToArray();
        var actualMatchedPlayIndexes = play
            .Select((number, index) => (number, index))
            .Where(item => winningSet.Contains(item.number))
            .Select(item => item.index)
            .ToArray();

        AssertSequence(actualMatchedWinningIndexes, matchedWinningIndexes, "matched winning indexes");
        AssertSequence(actualMatchedPlayIndexes, matchedPlayIndexes, "matched play indexes");
        AssertEqual(actualMatchedWinningIndexes.Length, actualMatchedPlayIndexes.Length, "match count agrees on both sides");

        long calculatedPrize;
        if (payoutSource == "play")
        {
            AssertEqual(4, playAmounts.Length, "play prize payload count");
            AssertEqual(0, winningAmounts.Length, "winning side has no prize amounts in payoutSource=play");
            AssertEqual(playAmounts.Length, playAmounts.Distinct().Count(), "play prize amounts unique when repetition is disabled");
            calculatedPrize = actualMatchedPlayIndexes.Sum(index => playAmounts[index]);
        }
        else
        {
            AssertEqual(2, winningAmounts.Length, "winning prize payload count");
            AssertEqual(0, playAmounts.Length, "play side has no prize amounts in payoutSource=winning");
            AssertEqual(winningAmounts.Length, winningAmounts.Distinct().Count(), "winning prize amounts unique when repetition is disabled");
            calculatedPrize = actualMatchedWinningIndexes.Sum(index => winningAmounts[index]);
        }

        AssertEqual(expectedPrize, calculatedPrize, "matched prize sum");
        if (expectedPrize == 0)
            AssertEqual(0, actualMatchedPlayIndexes.Length, "losing ticket has zero matches");
        else
            Assert(actualMatchedPlayIndexes.Length > 0, "winning ticket must contain at least one match");
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

    private static void AssertSequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, string label)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("Assertion failed: " + label);
    }
}
