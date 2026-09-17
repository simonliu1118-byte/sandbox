using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PackEditor;
using ScratchGame.Data;
using ScratchGame.Engine;
using ScratchGame.Models;
using ScratchGame.Services;

namespace ScratchGame.Regression;

internal static class Program
{
    private static readonly long[] ExpectedPrizeAmounts =
        [100, 500, 1_000, 2_500, 5_000, 10_000, 100_000];

    private const long ExpectedIssueSize = 8;
    private const long ExpectedTicketPrice = 500;
    private const long ExpectedTotalRedeemed = 119_100;
    private const long ExpectedTotalSpent = 4_000;
    private const long ExpectedFinalWallet = 215_100;

    public static async Task<int> Main(string[] args)
    {
        AppDatabase? database = null;
        var ownsDatabaseDirectory = false;
        try
        {
            var options = Options.Parse(args);
            RequireDedicatedCiEnvironment();
            Directory.CreateDirectory(options.WorkRoot);
            InstallRegressionBuiltInAssets();

            database = new AppDatabase();
            if (File.Exists(database.DatabasePath) || Directory.Exists(database.DataDirectory))
            {
                throw new InvalidOperationException(
                    $"Regression refuses to reuse an existing ScratchGame data directory: {database.DataDirectory}");
            }

            ownsDatabaseDirectory = true;
            await database.InitializeAsync();
            await new SeedDataService(database).EnsureSeedDataAsync();

            var loader = new ScratchPackV1Loader();
            var canonicalExtract = Path.Combine(options.WorkRoot, "canonical-load");
            var canonical = loader.LoadAndValidate(options.ScratchPackPath, canonicalExtract);
            AssertEqual("1", canonical.Ticket.GameType, "canonical GameType");
            AssertEqual(ExpectedIssueSize, canonical.Ticket.IssueSize, "canonical issueSize");
            AssertEqual(ExpectedTicketPrice, canonical.Ticket.Price, "canonical ticket price");
            AssertSequence(ExpectedPrizeAmounts, canonical.Ticket.Prizes.Select(p => p.Amount), "canonical prizes");
            Pass("ScratchPack V1 loader accepts the canonical ThreeStar-Test package");

            await RunImportedFinitePoolLifecycleAsync(database, canonical, options.ScratchPackPath);
            await RunBuiltInInstallContractAsync(database, options.WorkRoot);
            await RunPackEditorRoundTripAsync(database, options.WorkRoot);

            Console.WriteLine("REGRESSION PASS: ScratchPack / finite pool / wallet / PackEditor round-trip");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("REGRESSION FAIL");
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            if (ownsDatabaseDirectory && database is not null && Directory.Exists(database.DataDirectory))
            {
                try { Directory.Delete(database.DataDirectory, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static void RequireDedicatedCiEnvironment()
    {
        var githubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
        var explicitGate = Environment.GetEnvironmentVariable("SCRATCHGAME_REGRESSION");
        if (!string.Equals(githubActions, "true", StringComparison.OrdinalIgnoreCase) || explicitGate != "1")
        {
            throw new InvalidOperationException(
                "Regression runner may only use the dedicated GitHub Actions test environment. " +
                "Set SCRATCHGAME_REGRESSION=1 in the CI step.");
        }
    }

    private static async Task RunImportedFinitePoolLifecycleAsync(
        AppDatabase database,
        LoadedScratchPack canonical,
        string scratchPackPath)
    {
        var admin = new TicketAdminService(database);
        var catalog = new CatalogService(database);
        var pool = new PrizePoolService(database);
        var swap = new TicketSwapService(database);

        var ticketId = await admin.ImportScratchPackAsync(scratchPackPath);
        var adminItem = (await admin.GetTicketsAsync()).Single(item => item.Id == ticketId);
        Assert(adminItem.IsImported, "canonical TestPack must be installed as Imported");
        Assert(!adminItem.IsBuiltIn, "Imported package must not be marked BuiltIn");

        var detail = await admin.GetTicketDetailAsync(ticketId);
        AssertEqual(ExpectedIssueSize, detail.RemainingCount, "initial finite pool remaining");

        var user = (await catalog.GetUsersAsync()).Single();
        AssertEqual(AppDatabase.InitialWalletBalance, user.WalletBalance, "initial wallet");

        string PayloadFactory(long amount) => GamePayloadFactory.Create(canonical.Ticket, amount);

        var first = await pool.CreatePendingAsync(user.Id, ticketId, PayloadFactory);
        ValidatePendingPayload(first);
        detail = await admin.GetTicketDetailAsync(ticketId);
        AssertEqual(7L, detail.RemainingCount, "remaining after first purchase");

        user = (await catalog.GetUsersAsync()).Single(item => item.Id == user.Id);
        AssertEqual(99_500L, user.WalletBalance, "wallet after first purchase");
        AssertEqual(500L, user.TotalSpent, "total spent after first purchase");

        await AssertThrowsAsync<InvalidOperationException>(
            () => pool.CreatePendingAsync(user.Id, ticketId, PayloadFactory),
            "one user must not hold a second Pending Ticket");
        detail = await admin.GetTicketDetailAsync(ticketId);
        AssertEqual(7L, detail.RemainingCount, "rejected second Pending must not consume pool");

        var swapped = await swap.SwapPendingAsync(first.Id, PayloadFactory);
        Assert(swapped.Id != first.Id, "swap must replace the Pending Ticket identity");
        ValidatePendingPayload(swapped);
        detail = await admin.GetTicketDetailAsync(ticketId);
        AssertEqual(7L, detail.RemainingCount, "swap must keep finite pool remaining unchanged");

        user = (await catalog.GetUsersAsync()).Single(item => item.Id == user.Id);
        AssertEqual(99_500L, user.WalletBalance, "swap must not charge wallet again");
        AssertEqual(500L, user.TotalSpent, "swap must not increase total spent");

        await swap.MarkScratchStartedAsync(swapped.Id);
        var persisted = await catalog.GetPendingForUserAsync(user.Id);
        Assert(persisted is not null && !string.IsNullOrWhiteSpace(persisted.ScratchStateJson),
            "scratch start must be persisted on Pending Ticket");

        await AssertThrowsAsync<InvalidOperationException>(
            () => swap.SwapPendingAsync(swapped.Id, PayloadFactory),
            "swap after scratch start must be rejected");

        var redeemedAmounts = new List<long> { await pool.RedeemAsync(swapped.Id) };
        for (var index = 1; index < ExpectedIssueSize; index++)
        {
            var pending = await pool.CreatePendingAsync(user.Id, ticketId, PayloadFactory);
            ValidatePendingPayload(pending);
            await swap.MarkScratchStartedAsync(pending.Id);
            redeemedAmounts.Add(await pool.RedeemAsync(pending.Id));
        }

        AssertSequence(
            new[] { 0L }.Concat(ExpectedPrizeAmounts),
            redeemedAmounts.OrderBy(value => value),
            "full batch must redeem exactly one of every finite-pool outcome");

        detail = await admin.GetTicketDetailAsync(ticketId);
        AssertEqual(0L, detail.RemainingCount, "finite pool must reach zero after all tickets are issued");
        Assert(detail.PrizeRows.All(row => row.RemainingCount == 0), "every prize tier must reach zero remaining");
        Assert(!await catalog.HasRemainingTicketsAsync(ticketId), "sold-out batch must report no remaining tickets");
        await AssertThrowsAsync<InvalidOperationException>(
            () => pool.CreatePendingAsync(user.Id, ticketId, PayloadFactory),
            "sold-out batch must reject another purchase");

        Assert(await catalog.GetPendingForUserAsync(user.Id) is null, "no Pending Ticket may remain after settlement");
        user = (await catalog.GetUsersAsync()).Single(item => item.Id == user.Id);
        AssertEqual(ExpectedIssueSize, user.CompletedTicketCount, "completed ticket count");
        AssertEqual(7L, user.WinCount, "win count");
        AssertEqual(ExpectedTotalSpent, user.TotalSpent, "total spent");
        AssertEqual(ExpectedTotalRedeemed, user.TotalRedeemed, "total redeemed");
        AssertEqual(100_000L, user.MaxPrize, "max prize");
        AssertEqual(ExpectedFinalWallet, user.WalletBalance, "final wallet");
        AssertEqual(ExpectedTotalRedeemed - ExpectedTotalSpent, user.Net, "derived net result");

        Pass("Imported Pack: finite pool, purchase, swap gate, scratch, redeem, Wallet and cumulative statistics");
    }

    private static async Task RunBuiltInInstallContractAsync(AppDatabase database, string workRoot)
    {
        var packPath = Path.Combine(workRoot, "PackEditor-BuiltIn-Regression.scratchpack");
        var draft = CreateValidDraft("BuiltIn Regression");
        var packageId = draft.PackageId;
        _ = PackExportService.ExportAndRoundTripValidate(draft, packPath);

        var importer = new ScratchPackImporter(database);
        var firstId = await importer.InstallAsync(packPath, ScratchPackInstallSource.BuiltIn);
        await new CatalogService(database).EnsureInitialBatchAsync(firstId);
        var secondId = await importer.InstallAsync(packPath, ScratchPackInstallSource.BuiltIn);
        AssertEqual(firstId, secondId, "identical BuiltIn install must be idempotent");

        var item = (await new TicketAdminService(database).GetTicketsAsync()).Single(x => x.Id == firstId);
        Assert(item.IsBuiltIn, "BuiltIn install source must be persisted");
        Assert(!item.IsImported, "BuiltIn install must not be marked Imported");
        Assert(!item.CanUninstall, "BuiltIn Pack must not be uninstallable");
        AssertEqual(packageId.ToString("D"), item.SourcePackageId, "BuiltIn packageId");

        Pass("BuiltIn Pack installation source and idempotent reinstall contract");
    }

    private static async Task RunPackEditorRoundTripAsync(AppDatabase database, string workRoot)
    {
        var output = Path.Combine(workRoot, "PackEditor-Importer-RoundTrip.scratchpack");
        var draft = CreateValidDraft("PackEditor RoundTrip Regression");
        var packageId = draft.PackageId;

        var exported = PackExportService.ExportAndRoundTripValidate(draft, output);
        Assert(File.Exists(output) && new FileInfo(output).Length > 0, "PackEditor must create a non-empty .scratchpack");
        AssertEqual(packageId, exported.Manifest.PackageId, "PackEditor round-trip packageId");

        var verifyRoot = Path.Combine(workRoot, "packeditor-independent-loader");
        var independentlyLoaded = new ScratchPackV1Loader().LoadAndValidate(output, verifyRoot);
        AssertEqual(packageId, independentlyLoaded.Manifest.PackageId, "independent loader packageId");
        AssertEqual(ExpectedIssueSize, independentlyLoaded.Ticket.IssueSize, "PackEditor issueSize");

        var ticketId = await new TicketAdminService(database).ImportScratchPackAsync(output);
        var item = (await new TicketAdminService(database).GetTicketsAsync()).Single(x => x.Id == ticketId);
        Assert(item.IsImported, "PackEditor output must install through the normal Imported path");
        var detail = await new TicketAdminService(database).GetTicketDetailAsync(ticketId);
        AssertEqual(ExpectedIssueSize, detail.RemainingCount, "PackEditor imported finite pool size");

        Pass("PackEditor output -> authoritative loader -> ScratchGame Importer round-trip");
    }

    private static PackDraft CreateValidDraft(string name)
    {
        var draft = PackDraft.CreateNew(new Version(0, 5, 3));
        draft.Name = name;
        draft.Author = "ScratchGame Regression";
        draft.GameType = "1";
        draft.Price = ExpectedTicketPrice;
        draft.Canvas = 1;
        draft.GridSize = 3;
        draft.AllowNearMiss = true;
        draft.IssueSize = ExpectedIssueSize;
        draft.TicketsPerBook = ExpectedIssueSize;

        draft.TicketArt.Source = "builtin";
        draft.TicketArt.Reference = "01-blue";
        draft.Foil.Source = "builtin";
        draft.Foil.Reference = "brushed-silver-three-star";

        draft.ZoneX = 219;
        draft.ZoneY = 256;
        draft.ZoneWidth = 191;
        draft.ZoneHeight = 138;
        draft.HorizontalGap = 34;
        draft.VerticalGap = 28;
        draft.ZoneShape = "roundedRectangle";
        draft.CornerRadius = 12;

        draft.PriceDisplay = true;
        SetRect(draft.PriceArea, 852, 43, 201, 82);
        SetRect(draft.SerialArea, 364, 774, 350, 59);

        draft.EnsurePrizeTiers();
        AssertEqual(ExpectedPrizeAmounts.Length, draft.Prizes.Count, "PackEditor GameType 1 tier count");
        for (var index = 0; index < ExpectedPrizeAmounts.Length; index++)
        {
            draft.Prizes[index].Amount = ExpectedPrizeAmounts[index];
            draft.Prizes[index].Count = 1;
        }

        return draft;
    }

    private static void ValidatePendingPayload(PendingTicket pending)
    {
        using var document = JsonDocument.Parse(pending.PayloadJson);
        var root = document.RootElement;
        AssertEqual("1", root.GetProperty("gameType").GetString(), "payload gameType");
        AssertEqual(pending.ReservedAmount, root.GetProperty("prizeAmount").GetInt64(), "payload prize amount");
        AssertEqual(
            root.GetProperty("targetLineCount").GetInt32(),
            root.GetProperty("actualLineCount").GetInt32(),
            "GameType 1 actual line count");
    }

    private static void InstallRegressionBuiltInAssets()
    {
        var ticket = Path.Combine(
            AppContext.BaseDirectory,
            "BuiltInAssets", "Tickets", "gameType1", "01-blue.png");
        var foil = Path.Combine(
            AppContext.BaseDirectory,
            "BuiltInAssets", "Foils", "brushed-silver-three-star.png");

        WriteFixturePng(ticket, 1080, 882);
        WriteFixturePng(foil, 32, 32);
    }

    private static void WriteFixturePng(string path, int width, int height)
    {
        if (File.Exists(path))
            throw new InvalidOperationException($"Regression refuses to overwrite an existing runtime asset: {path}");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
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

    private static void SetRect(EditorRect rect, int x, int y, int width, int height)
    {
        rect.X = x;
        rect.Y = y;
        rect.Width = width;
        rect.Height = height;
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action, string label)
        where TException : Exception
    {
        try
        {
            await action();
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
        {
            throw new InvalidOperationException(
                $"Assertion failed: {label}; expected={expected}; actual={actual}");
        }
    }

    private static void AssertSequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, string label)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("Assertion failed: " + label);
    }

    private static void Pass(string message)
        => Console.WriteLine("PASS: " + message);

    private sealed record Options(string ScratchPackPath, string WorkRoot)
    {
        public static Options Parse(string[] args)
        {
            string? scratchPack = null;
            string? workRoot = null;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--scratchpack" when index + 1 < args.Length:
                        scratchPack = args[++index];
                        break;
                    case "--work-root" when index + 1 < args.Length:
                        workRoot = args[++index];
                        break;
                    default:
                        throw new ArgumentException($"Unknown or incomplete argument: {args[index]}");
                }
            }

            if (string.IsNullOrWhiteSpace(scratchPack))
                throw new ArgumentException("--scratchpack is required.");
            var fullPack = Path.GetFullPath(scratchPack);
            if (!File.Exists(fullPack))
                throw new FileNotFoundException("Regression ScratchPack not found.", fullPack);

            var fullWork = Path.GetFullPath(
                string.IsNullOrWhiteSpace(workRoot)
                    ? Path.Combine(Path.GetTempPath(), "ScratchGameRegression", Guid.NewGuid().ToString("N"))
                    : workRoot);
            return new Options(fullPack, fullWork);
        }
    }
}
