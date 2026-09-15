using System.IO.Compression;
using System.Text.Json;
using ScratchGame.Data;

namespace ScratchGame.Services;

public sealed class ScratchPackImporter(AppDatabase database)
{
    private const long MaxPackageBytes = 50L * 1024 * 1024;
    private const long MaxSingleFileBytes = 20L * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".png", ".wav"
    };
    private static readonly HashSet<string> SupportedRules = new(StringComparer.Ordinal)
    {
        "1", "LuckyNumberMatch", "ThreeLine", "MatchThree"
    };

    public async Task<string> ImportAsync(string scratchPackPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(scratchPackPath))
            throw new FileNotFoundException("找不到 ScratchPack。", scratchPackPath);
        if (!string.Equals(Path.GetExtension(scratchPackPath), ".scratchpack", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("彩券包副檔名必須是 .scratchpack。");

        var tempRoot = Path.Combine(Path.GetTempPath(), "ScratchGame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string? finalPackageDirectory = null;
        try
        {
            using var archive = ZipFile.OpenRead(scratchPackPath);
            ValidateArchiveEntries(archive);
            archive.ExtractToDirectory(tempRoot, overwriteFiles: false);

            using var manifest = ReadJson(Path.Combine(tempRoot, "manifest.json"));
            var formatVersion = RequiredString(manifest.RootElement, "formatVersion");
            if (formatVersion != "1.0")
                throw new InvalidDataException($"不支援的 ScratchPack formatVersion：{formatVersion}");

            var packageIdText = RequiredString(manifest.RootElement, "packageId");
            if (!Guid.TryParse(packageIdText, out var packageId))
                throw new InvalidDataException("manifest.packageId 必須是有效 GUID。");
            ValidateMinimumAppVersion(RequiredString(manifest.RootElement, "minimumAppVersion"));

            var ticketRelative = ValidateRelativeJsonPath(RequiredString(manifest.RootElement, "ticketFile"));
            var layoutRelative = ValidateRelativeJsonPath(RequiredString(manifest.RootElement, "layoutFile"));
            var prizesRelative = ValidateRelativeJsonPath(RequiredString(manifest.RootElement, "prizesFile"));

            using var ticketDoc = ReadJson(ResolveInside(tempRoot, ticketRelative));
            using var layoutDoc = ReadJson(ResolveInside(tempRoot, layoutRelative));
            using var prizesDoc = ReadJson(ResolveInside(tempRoot, prizesRelative));

            var ticket = ParseAndValidateTicket(ticketDoc.RootElement, tempRoot);
            var tiers = ParseAndValidatePrizes(prizesDoc.RootElement, ticket.IssueSize);
            ValidateLayout(layoutDoc.RootElement, ticket.RuleId);

            var winningCount = tiers.Where(t => t.Amount > 0).Sum(t => t.Count);
            var winRate = (double)winningCount / ticket.IssueSize;

            var packageDirectory = Path.Combine(database.DataDirectory, "packages");
            Directory.CreateDirectory(packageDirectory);
            finalPackageDirectory = Path.Combine(packageDirectory, packageId.ToString("D"));

            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();

            var collision = connection.CreateCommand();
            collision.Transaction = transaction;
            collision.CommandText = """
                SELECT COUNT(*)
                FROM ticket_definitions
                WHERE id = $ticketId OR source_package_id = $packageId;
                """;
            collision.Parameters.AddWithValue("$ticketId", ticket.TicketId);
            collision.Parameters.AddWithValue("$packageId", packageId.ToString("D"));
            if (Convert.ToInt64(await collision.ExecuteScalarAsync(cancellationToken)) > 0)
                throw new InvalidOperationException("相同 ticketId 或 packageId 的彩券已存在。");

            var styleCommand = connection.CreateCommand();
            styleCommand.Transaction = transaction;
            styleCommand.CommandText = "SELECT COALESCE(MAX(style_number), 0) + 1 FROM ticket_metadata WHERE style_number > 0;";
            var styleNumber = Convert.ToInt64(await styleCommand.ExecuteScalarAsync(cancellationToken));
            if (styleNumber <= 0) styleNumber = 1;

            if (Directory.Exists(finalPackageDirectory))
                Directory.Delete(finalPackageDirectory, recursive: true);
            Directory.Move(tempRoot, finalPackageDirectory);
            tempRoot = string.Empty;

            try
            {
                var addTicket = connection.CreateCommand();
                addTicket.Transaction = transaction;
                addTicket.CommandText = """
                    INSERT INTO ticket_definitions(
                        id, display_name, price, rule_id, issue_size,
                        published_win_rate, enabled, locked, source_package_id, created_utc)
                    VALUES($id, $name, $price, $ruleId, $issueSize,
                           $winRate, 1, 0, $packageId, $createdUtc);
                    """;
                addTicket.Parameters.AddWithValue("$id", ticket.TicketId);
                addTicket.Parameters.AddWithValue("$name", ticket.DisplayName);
                addTicket.Parameters.AddWithValue("$price", ticket.Price);
                addTicket.Parameters.AddWithValue("$ruleId", ticket.RuleId);
                addTicket.Parameters.AddWithValue("$issueSize", ticket.IssueSize);
                addTicket.Parameters.AddWithValue("$winRate", winRate);
                addTicket.Parameters.AddWithValue("$packageId", packageId.ToString("D"));
                addTicket.Parameters.AddWithValue("$createdUtc", DateTimeOffset.UtcNow.ToString("O"));
                await addTicket.ExecuteNonQueryAsync(cancellationToken);

                var addMetadata = connection.CreateCommand();
                addMetadata.Transaction = transaction;
                addMetadata.CommandText = """
                    INSERT INTO ticket_metadata(ticket_id, style_number, tickets_per_book, price_display)
                    VALUES($ticketId, $styleNumber, $ticketsPerBook, $priceDisplay);
                    """;
                addMetadata.Parameters.AddWithValue("$ticketId", ticket.TicketId);
                addMetadata.Parameters.AddWithValue("$styleNumber", styleNumber);
                addMetadata.Parameters.AddWithValue("$ticketsPerBook", ticket.TicketsPerBook);
                addMetadata.Parameters.AddWithValue("$priceDisplay", ticket.PriceDisplay);
                await addMetadata.ExecuteNonQueryAsync(cancellationToken);

                foreach (var tier in tiers)
                {
                    var addTier = connection.CreateCommand();
                    addTier.Transaction = transaction;
                    addTier.CommandText = """
                        INSERT INTO prize_tiers(ticket_id, tier_id, amount, initial_count, sort_order)
                        VALUES($ticketId, $tierId, $amount, $count, $sortOrder);
                        """;
                    addTier.Parameters.AddWithValue("$ticketId", ticket.TicketId);
                    addTier.Parameters.AddWithValue("$tierId", tier.Id);
                    addTier.Parameters.AddWithValue("$amount", tier.Amount);
                    addTier.Parameters.AddWithValue("$count", tier.Count);
                    addTier.Parameters.AddWithValue("$sortOrder", tier.SortOrder);
                    await addTier.ExecuteNonQueryAsync(cancellationToken);
                }
                transaction.Commit();
            }
            catch
            {
                if (Directory.Exists(finalPackageDirectory))
                    Directory.Delete(finalPackageDirectory, recursive: true);
                throw;
            }

            return ticket.TicketId;
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempRoot) && Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void ValidateArchiveEntries(ZipArchive archive)
    {
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (name.StartsWith('/') || name.Contains(":", StringComparison.Ordinal) || name.Split('/').Any(part => part == ".."))
                throw new InvalidDataException($"ScratchPack 含不安全路徑：{entry.FullName}");
            if (string.IsNullOrEmpty(entry.Name)) continue;

            var extension = Path.GetExtension(entry.Name);
            if (!AllowedExtensions.Contains(extension))
                throw new InvalidDataException($"ScratchPack 含不允許的檔案類型：{entry.FullName}");
            if (entry.Length > MaxSingleFileBytes)
                throw new InvalidDataException($"ScratchPack 單一檔案過大：{entry.FullName}");
            total += entry.Length;
            if (total > MaxPackageBytes)
                throw new InvalidDataException("ScratchPack 解壓後總大小超過 50 MB。");
        }

        var names = archive.Entries.Select(e => e.FullName.Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);
        foreach (var required in new[] { "manifest.json", "ticket.json", "layout.json", "prizes.json" })
            if (!names.Contains(required))
                throw new InvalidDataException($"ScratchPack 缺少必要檔案：{required}");
    }

    private static JsonDocument ReadJson(string path)
    {
        if (!File.Exists(path))
            throw new InvalidDataException($"找不到必要 JSON：{Path.GetFileName(path)}");
        try { return JsonDocument.Parse(File.ReadAllBytes(path)); }
        catch (JsonException ex) { throw new InvalidDataException($"JSON 格式錯誤：{Path.GetFileName(path)}", ex); }
    }

    private static TicketImportDefinition ParseAndValidateTicket(JsonElement root, string packageRoot)
    {
        var ticketId = RequiredString(root, "ticketId");
        if (ticketId.Length > 80 || ticketId.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '-' or '_')))
            throw new InvalidDataException("ticketId 只能使用英數、-、_，且最多 80 字元。");

        var displayName = RequiredString(root, "displayName").Trim();
        if (displayName.Length is < 1 or > 50)
            throw new InvalidDataException("displayName 長度必須為 1～50 字元。");

        var price = RequiredInt64(root, "price");
        var issueSize = RequiredInt64(root, "issueSize");
        var ruleId = RequiredString(root, "ruleId");
        if (price <= 0 || issueSize <= 0)
            throw new InvalidDataException("price 與 issueSize 必須大於 0。");
        if (!SupportedRules.Contains(ruleId))
            throw new InvalidDataException($"目前版本不支援 Game Rule：{ruleId}");

        var ticketsPerBook = OptionalInt64(root, "ticketsPerBook") ?? issueSize;
        if (ticketsPerBook <= 0)
            throw new InvalidDataException("ticketsPerBook 必須大於 0。");
        if (issueSize % ticketsPerBook != 0)
            throw new InvalidDataException("issueSize 必須可以被 ticketsPerBook 整除。");

        var priceDisplay = (int)(OptionalInt64(root, "priceDisplay") ?? 0);
        if (priceDisplay is not (0 or 1))
            throw new InvalidDataException("priceDisplay 目前只允許 0 或 1。");

        if (!root.TryGetProperty("art", out var art))
            throw new InvalidDataException("ticket.json 缺少 art。");
        var background = ValidateRelativeResourcePath(RequiredString(art, "background"), ".png");
        if (!File.Exists(ResolveInside(packageRoot, background)))
            throw new InvalidDataException("ticket.json 指定的背景 PNG 不存在。");

        if (art.TryGetProperty("scratchMask", out var maskElement) && maskElement.ValueKind == JsonValueKind.String)
        {
            var maskText = maskElement.GetString();
            if (!string.IsNullOrWhiteSpace(maskText))
            {
                var mask = ValidateRelativeResourcePath(maskText, ".png");
                if (!File.Exists(ResolveInside(packageRoot, mask)))
                    throw new InvalidDataException("ticket.json 指定的刮膜 PNG 不存在。");
            }
        }

        if (root.TryGetProperty("scratch", out var scratch))
        {
            if (scratch.TryGetProperty("completionRatio", out var ratioElement))
            {
                var ratio = ratioElement.GetDouble();
                if (ratio is < 0.50 or > 0.95)
                    throw new InvalidDataException("scratch.completionRatio 必須介於 0.50～0.95。");
            }
            if (scratch.TryGetProperty("brushRadius", out var radiusElement))
            {
                var radius = radiusElement.GetDouble();
                if (radius is < 8 or > 80)
                    throw new InvalidDataException("scratch.brushRadius 必須介於 8～80。");
            }
        }

        return new TicketImportDefinition(ticketId, displayName, price, ruleId, issueSize, ticketsPerBook, priceDisplay);
    }

    private static List<PrizeImportTier> ParseAndValidatePrizes(JsonElement root, long issueSize)
    {
        if (!root.TryGetProperty("tiers", out var tiersElement) || tiersElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("prizes.json 缺少 tiers array。");
        var result = new List<PrizeImportTier>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var order = 0;
        foreach (var tier in tiersElement.EnumerateArray())
        {
            var id = RequiredString(tier, "id");
            var amount = RequiredInt64(tier, "amount");
            var count = RequiredInt64(tier, "count");
            if (!ids.Add(id)) throw new InvalidDataException($"重複的 prize tier id：{id}");
            if (amount < 0 || count <= 0) throw new InvalidDataException("Prize amount 必須 >= 0，count 必須 > 0。");
            result.Add(new PrizeImportTier(id, amount, count, order++));
        }
        if (result.Count == 0) throw new InvalidDataException("prizes.json 至少需要一個獎項。");
        if (result.Sum(t => t.Count) != issueSize)
            throw new InvalidDataException("全部 prize count 加總必須精確等於 issueSize。");
        return result;
    }

    private static void ValidateLayout(JsonElement root, string ruleId)
    {
        if (!root.TryGetProperty("canvas", out var canvas)) throw new InvalidDataException("layout.json 缺少 canvas。");
        var width = RequiredInt64(canvas, "width");
        var height = RequiredInt64(canvas, "height");
        if (width <= 0 || height <= 0) throw new InvalidDataException("canvas width/height 必須大於 0。");

        if (!root.TryGetProperty("scratchZones", out var zones) || zones.ValueKind != JsonValueKind.Array || zones.GetArrayLength() == 0)
            throw new InvalidDataException("layout.json 至少需要一個 scratchZone。");
        var zoneIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var zone in zones.EnumerateArray())
        {
            var id = RequiredString(zone, "id");
            if (!zoneIds.Add(id)) throw new InvalidDataException($"重複的 scratchZone id：{id}");
            ValidateRect(zone, width, height, $"scratchZone {id}");
        }

        if (!root.TryGetProperty("rule", out var rule)) throw new InvalidDataException("layout.json 缺少 rule 設定。");
        switch (ruleId)
        {
            case "LuckyNumberMatch":
                RequireProperty(rule, "winningNumberArea"); RequireProperty(rule, "playArea");
                RequireProperty(rule, "winningNumberCount"); RequireProperty(rule, "playNumberCount");
                RequireProperty(rule, "numberMin"); RequireProperty(rule, "numberMax");
                break;
            case "1":
            case "ThreeLine":
                RequireProperty(rule, "grid");
                break;
            case "MatchThree":
                RequireProperty(rule, "grid"); RequireProperty(rule, "matchCount");
                break;
        }
    }

    private static void ValidateRect(JsonElement rect, long canvasWidth, long canvasHeight, string label)
    {
        var x = RequiredInt64(rect, "x"); var y = RequiredInt64(rect, "y");
        var width = RequiredInt64(rect, "width"); var height = RequiredInt64(rect, "height");
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > canvasWidth || y + height > canvasHeight)
            throw new InvalidDataException($"{label} 超出 canvas 或尺寸無效。");
    }

    private static void ValidateMinimumAppVersion(string minimumAppVersion)
    {
        if (!Version.TryParse(minimumAppVersion, out var minimum)) throw new InvalidDataException("minimumAppVersion 必須是 X.Y.Z。");
        var current = new Version(0, 3, 0);
        if (minimum > current) throw new InvalidDataException($"此 ScratchPack 需要 ScratchGame {minimumAppVersion} 以上版本。");
    }

    private static string ValidateRelativeJsonPath(string path) => ValidateRelativeResourcePath(path, ".json");
    private static string ValidateRelativeResourcePath(string path, string requiredExtension)
    {
        path = path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains(":", StringComparison.Ordinal) || path.Split('/').Any(part => part == ".."))
            throw new InvalidDataException($"不合法的相對路徑：{path}");
        if (!string.Equals(Path.GetExtension(path), requiredExtension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"資源必須是 {requiredExtension}：{path}");
        return path;
    }

    private static string ResolveInside(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("ScratchPack 資源路徑超出套件根目錄。");
        return fullPath;
    }

    private static string RequiredString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"缺少字串欄位：{property}");
        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException($"欄位不可空白：{property}");
        return text;
    }
    private static long RequiredInt64(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || !value.TryGetInt64(out var result))
            throw new InvalidDataException($"缺少整數欄位：{property}");
        return result;
    }
    private static long? OptionalInt64(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        if (!value.TryGetInt64(out var result)) throw new InvalidDataException($"欄位必須是整數：{property}");
        return result;
    }
    private static void RequireProperty(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out _)) throw new InvalidDataException($"Game Rule 缺少必要欄位：{property}");
    }

    private sealed record TicketImportDefinition(string TicketId, string DisplayName, long Price, string RuleId, long IssueSize, long TicketsPerBook, int PriceDisplay);
    private sealed record PrizeImportTier(string Id, long Amount, long Count, int SortOrder);
}
