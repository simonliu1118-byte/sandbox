using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using ScratchGame.Models;
using ScratchGame.Services;

namespace PackEditor;

internal static class PackExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static LoadedScratchPack ExportAndRoundTripValidate(PackDraft draft, string outputPath)
    {
        var sessionRoot = Path.Combine(Path.GetTempPath(), "ScratchGame", "PackEditor", Guid.NewGuid().ToString("N"));
        var stagingRoot = Path.Combine(sessionRoot, "staging");
        var verifyRoot = Path.Combine(sessionRoot, "verify");
        var temporaryPack = Path.Combine(sessionRoot, "output.scratchpack");

        Directory.CreateDirectory(stagingRoot);
        try
        {
            WriteManifest(draft, stagingRoot);
            WriteTicket(draft, stagingRoot);
            CopyPackageAssets(draft, stagingRoot);

            ZipFile.CreateFromDirectory(stagingRoot, temporaryPack, CompressionLevel.Optimal, includeBaseDirectory: false);
            var loaded = new ScratchPackV1Loader().LoadAndValidate(temporaryPack, verifyRoot);

            var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrWhiteSpace(outputDirectory))
                Directory.CreateDirectory(outputDirectory);
            File.Copy(temporaryPack, outputPath, overwrite: true);
            return loaded;
        }
        finally
        {
            try
            {
                if (Directory.Exists(sessionRoot))
                    Directory.Delete(sessionRoot, recursive: true);
            }
            catch
            {
                // Temporary cleanup failure must not invalidate an already verified output file.
            }
        }
    }

    private static void WriteManifest(PackDraft draft, string root)
    {
        var manifest = new JsonObject
        {
            ["formatVersion"] = "1.0",
            ["packageId"] = draft.PackageId.ToString("D"),
            ["author"] = draft.Author,
            ["minimumAppVersion"] = FormatVersion(draft.MinimumAppVersion),
            ["ticketFile"] = "ticket.json"
        };
        File.WriteAllText(Path.Combine(root, "manifest.json"), manifest.ToJsonString(JsonOptions));
    }

    private static void WriteTicket(PackDraft draft, string root)
    {
        var ticket = new JsonObject
        {
            ["name"] = draft.Name,
            ["price"] = draft.Price,
            ["canvas"] = draft.Canvas,
            ["priceDisplay"] = draft.PriceDisplay ? 1 : 0,
            ["gameType"] = draft.GameType,
            ["issueSize"] = draft.IssueSize,
            ["ticketsPerBook"] = draft.TicketsPerBook,
            ["art"] = new JsonObject
            {
                ["ticket"] = ResourceRefNode(draft.TicketArt.ToResourceRef())
            },
            ["serialDisplayArea"] = RectNode(draft.SerialArea.ToRect()),
            ["scratch"] = new JsonObject
            {
                ["foil"] = ResourceRefNode(draft.Foil.ToResourceRef()),
                ["zones"] = new JsonArray(draft.BuildZones().Select(ZoneNode).ToArray())
            },
            ["game"] = new JsonObject
            {
                ["gridSize"] = draft.GridSize,
                ["allowNearMiss"] = draft.AllowNearMiss
            },
            ["prizes"] = new JsonArray(draft.BuildPrizes().Select(PrizeNode).ToArray())
        };

        if (draft.PriceDisplay)
            ticket["priceDisplayArea"] = RectNode(draft.PriceArea.ToRect());

        File.WriteAllText(Path.Combine(root, "ticket.json"), ticket.ToJsonString(JsonOptions));
    }

    private static void CopyPackageAssets(PackDraft draft, string root)
    {
        var assetsRoot = Path.Combine(root, "assets");
        var copied = false;

        if (draft.TicketArt.Source == "package")
        {
            if (string.IsNullOrWhiteSpace(draft.TicketArt.ExternalPath))
                throw new InvalidDataException("自訂票面缺少來源 PNG。");
            Directory.CreateDirectory(assetsRoot);
            File.Copy(draft.TicketArt.ExternalPath, Path.Combine(assetsRoot, "ticket.png"), overwrite: true);
            copied = true;
        }

        if (draft.Foil.Source == "package")
        {
            if (string.IsNullOrWhiteSpace(draft.Foil.ExternalPath))
                throw new InvalidDataException("自訂銀膜缺少來源 PNG。");
            Directory.CreateDirectory(assetsRoot);
            File.Copy(draft.Foil.ExternalPath, Path.Combine(assetsRoot, "foil.png"), overwrite: true);
            copied = true;
        }

        if (!copied && Directory.Exists(assetsRoot))
            Directory.Delete(assetsRoot);
    }

    private static JsonObject ResourceRefNode(ScratchPackResourceRef resource)
        => new()
        {
            ["source"] = resource.Source,
            ["ref"] = resource.Ref
        };

    private static JsonObject RectNode(ScratchPackRect rect)
        => new()
        {
            ["x"] = rect.X,
            ["y"] = rect.Y,
            ["width"] = rect.Width,
            ["height"] = rect.Height
        };

    private static JsonObject ZoneNode(ScratchPackZone zone)
    {
        var node = new JsonObject
        {
            ["id"] = zone.Id,
            ["x"] = zone.X,
            ["y"] = zone.Y,
            ["width"] = zone.Width,
            ["height"] = zone.Height,
            ["shape"] = zone.Shape
        };
        if (zone.CornerRadius.HasValue)
            node["cornerRadius"] = zone.CornerRadius.Value;
        return node;
    }

    private static JsonObject PrizeNode(ScratchPackPrize prize)
        => new()
        {
            ["amount"] = prize.Amount,
            ["count"] = prize.Count
        };

    private static string FormatVersion(Version version)
        => $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
}
