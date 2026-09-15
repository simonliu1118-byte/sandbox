using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ScratchGame.Models;

namespace ScratchGame.Services;

public sealed class ScratchPackV1Loader
{
    private const long MaxPackageBytes = 50L * 1024 * 1024;
    private const long MaxSingleFileBytes = 20L * 1024 * 1024;
    private const int Canvas1Width = 1080;
    private const int Canvas1Height = 882;

    private static readonly HashSet<string> BuiltInFoils = new(StringComparer.Ordinal)
    {
        "brushed-silver-plain",
        "brushed-silver-three-star"
    };

    private static readonly Dictionary<string, HashSet<string>> BuiltInTickets = new(StringComparer.Ordinal)
    {
        ["1"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "01-red",
            "01-blue",
            "02"
        }
    };

    public LoadedScratchPack LoadAndValidate(string scratchPackPath, string extractionRoot)
    {
        if (!File.Exists(scratchPackPath))
            throw new FileNotFoundException("找不到 ScratchPack。", scratchPackPath);
        if (!string.Equals(Path.GetExtension(scratchPackPath), ".scratchpack", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("彩券包副檔名必須是 .scratchpack。");

        using (var archive = ZipFile.OpenRead(scratchPackPath))
        {
            ValidateArchiveEntries(archive);
            archive.ExtractToDirectory(extractionRoot, overwriteFiles: false);
        }

        var manifestPath = Path.Combine(extractionRoot, "manifest.json");
        var manifestJson = File.ReadAllText(manifestPath);
        var manifest = ParseManifest(manifestJson);
        ValidateMinimumAppVersion(manifest.MinimumAppVersion);

        var ticketPath = ResolveInside(extractionRoot, manifest.TicketFile);
        var ticketJson = File.ReadAllText(ticketPath);
        var ticket = ParseTicket(ticketJson, extractionRoot);

        return new LoadedScratchPack(
            manifest,
            ticket,
            manifestJson,
            ComputeSha256(scratchPackPath));
    }

    public ScratchPackTicketDefinition LoadInstalledTicket(string packageRoot)
    {
        var ticketPath = ResolveInside(packageRoot, "ticket.json");
        if (!File.Exists(ticketPath))
            throw new InvalidDataException("已安裝 ScratchPack 缺少 ticket.json。");
        return ParseTicket(File.ReadAllText(ticketPath), packageRoot);
    }

    public static string ResolveBuiltInTicketPath(string gameType, string reference)
    {
        if (!IsBuiltInTicketSupported(gameType, reference))
            throw new InvalidDataException($"不支援的 built-in 票面：GameType {gameType} / {reference}");
        return Path.Combine(
            AppContext.BaseDirectory,
            "BuiltInAssets",
            "Tickets",
            $"gameType{gameType}",
            reference + ".png");
    }

    public static string ResolveBuiltInFoilPath(string reference)
    {
        if (!BuiltInFoils.Contains(reference))
            throw new InvalidDataException($"不支援的 built-in 銀膜：{reference}");
        return Path.Combine(
            AppContext.BaseDirectory,
            "BuiltInAssets",
            "Foils",
            reference + ".png");
    }

    public static bool IsBuiltInTicketSupported(string gameType, string reference)
        => BuiltInTickets.TryGetValue(gameType, out var refs) && refs.Contains(reference);

    public static bool IsBuiltInFoilSupported(string reference)
        => BuiltInFoils.Contains(reference);

    private static void ValidateArchiveEntries(ZipArchive archive)
    {
        long total = 0;
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(name))
                continue;
            if (!names.Add(name))
                throw new InvalidDataException($"ScratchPack 含重複路徑：{name}");
            if (name.StartsWith('/') || name.Contains(':', StringComparison.Ordinal) ||
                name.Split('/').Any(part => part is ".." or "."))
                throw new InvalidDataException($"ScratchPack 含不安全路徑：{entry.FullName}");
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var extension = Path.GetExtension(entry.Name);
            if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"ScratchPack V1 含不允許的檔案類型：{entry.FullName}");

            if (entry.Length > MaxSingleFileBytes)
                throw new InvalidDataException($"ScratchPack 單一檔案過大：{entry.FullName}");
            total += entry.Length;
            if (total > MaxPackageBytes)
                throw new InvalidDataException("ScratchPack 解壓後總大小超過 50 MB。");

            if (name.EndsWith("thumbnail.png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("ScratchPack V1 不接受 thumbnail.png。");
        }

        foreach (var required in new[] { "manifest.json", "ticket.json" })
        {
            if (!names.Contains(required))
                throw new InvalidDataException($"ScratchPack 缺少必要檔案：{required}");
        }
    }

    private static ScratchPackManifestDefinition ParseManifest(string json)
    {
        using var doc = ParseJson(json, "manifest.json");
        var root = doc.RootElement;
        EnsureObject(root, "manifest.json");
        EnsureOnlyProperties(root, "manifest.json",
            "formatVersion", "packageId", "author", "minimumAppVersion", "ticketFile");

        var formatVersion = RequiredString(root, "formatVersion");
        if (formatVersion != "1.0")
            throw new InvalidDataException($"不支援的 ScratchPack formatVersion：{formatVersion}");

        if (!Guid.TryParse(RequiredString(root, "packageId"), out var packageId))
            throw new InvalidDataException("manifest.packageId 必須是有效 GUID。");

        var author = RequiredStringAllowEmpty(root, "author");
        if (!Version.TryParse(RequiredString(root, "minimumAppVersion"), out var minimum))
            throw new InvalidDataException("minimumAppVersion 必須是 X.Y.Z。");

        var ticketFile = ValidateRelativeResourcePath(RequiredString(root, "ticketFile"), ".json");
        if (!string.Equals(ticketFile, "ticket.json", StringComparison.Ordinal))
            throw new InvalidDataException("ScratchPack V1 的 ticketFile 必須指向根目錄 ticket.json。");

        return new ScratchPackManifestDefinition(formatVersion, packageId, author, minimum, ticketFile);
    }

    private static ScratchPackTicketDefinition ParseTicket(string json, string packageRoot)
    {
        using var doc = ParseJson(json, "ticket.json");
        var root = doc.RootElement;
        EnsureObject(root, "ticket.json");
        EnsureOnlyProperties(root, "ticket.json",
            "name", "price", "canvas", "priceDisplay", "priceDisplayArea",
            "gameType", "issueSize", "ticketsPerBook", "art", "serialDisplayArea",
            "scratch", "game", "prizes");

        var name = RequiredString(root, "name").Trim();
        if (name.Length is < 1 or > 60)
            throw new InvalidDataException("ticket.name 長度必須為 1～60 字元。");

        var price = RequiredInt64(root, "price");
        var canvas = RequiredInt32(root, "canvas");
        var priceDisplay = RequiredInt32(root, "priceDisplay");
        var gameType = RequiredString(root, "gameType");
        var issueSize = RequiredInt64(root, "issueSize");
        var ticketsPerBook = RequiredInt64(root, "ticketsPerBook");

        if (price <= 0)
            throw new InvalidDataException("price 必須大於 0。");
        if (canvas != 1)
            throw new InvalidDataException($"目前版本不支援 Canvas code：{canvas}");
        if (priceDisplay is not 0 and not 1)
            throw new InvalidDataException("priceDisplay 只允許 0 或 1。");
        if (issueSize <= 0 || ticketsPerBook <= 0 || issueSize % ticketsPerBook != 0)
            throw new InvalidDataException("issueSize / ticketsPerBook 無效或無法整除。");
        if (gameType != "1")
            throw new InvalidDataException($"目前版本尚未實作 GameType：{gameType}");

        ScratchPackRect? priceArea = null;
        if (priceDisplay == 1)
        {
            if (!root.TryGetProperty("priceDisplayArea", out var priceAreaElement))
                throw new InvalidDataException("priceDisplay=1 時必須提供 priceDisplayArea。");
            priceArea = ParseRect(priceAreaElement, "priceDisplayArea");
            ValidateCanvasRect(priceArea, "priceDisplayArea");
        }
        else if (root.TryGetProperty("priceDisplayArea", out _))
        {
            throw new InvalidDataException("priceDisplay=0 時不得提供 priceDisplayArea。");
        }

        var serialArea = ParseRect(RequiredProperty(root, "serialDisplayArea"), "serialDisplayArea");
        ValidateCanvasRect(serialArea, "serialDisplayArea");

        var art = RequiredProperty(root, "art");
        EnsureObject(art, "art");
        EnsureOnlyProperties(art, "art", "ticket");
        var ticketArt = ParseResourceRef(RequiredProperty(art, "ticket"), "art.ticket");
        ValidateTicketResource(ticketArt, gameType, canvas, packageRoot);

        var scratch = RequiredProperty(root, "scratch");
        EnsureObject(scratch, "scratch");
        EnsureOnlyProperties(scratch, "scratch", "foil", "zones");
        var foil = ParseResourceRef(RequiredProperty(scratch, "foil"), "scratch.foil");
        ValidateFoilResource(foil, packageRoot);

        if (!scratch.TryGetProperty("zones", out var zonesElement) || zonesElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("scratch.zones 必須是 array。");
        var zones = zonesElement.EnumerateArray()
            .Select((element, index) => ParseZone(element, index))
            .ToArray();

        var game = RequiredProperty(root, "game");
        EnsureObject(game, "game");
        EnsureOnlyProperties(game, "game", "gridSize", "allowNearMiss");
        var gridSize = RequiredInt32(game, "gridSize");
        if (gridSize is not 3 and not 4 and not 5)
            throw new InvalidDataException("GameType 1 的 gridSize 目前只支援 3、4、5。");
        var allowNearMiss = game.TryGetProperty("allowNearMiss", out var nearMiss)
            ? RequiredBoolean(nearMiss, "game.allowNearMiss")
            : false;
        ValidateGameType1Zones(zones, gridSize);

        if (!root.TryGetProperty("prizes", out var prizesElement) || prizesElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("prizes 必須是 array。");
        var prizes = ParsePrizes(prizesElement, issueSize, gridSize);

        return new ScratchPackTicketDefinition(
            name,
            price,
            canvas,
            priceDisplay == 1,
            priceArea,
            gameType,
            issueSize,
            ticketsPerBook,
            ticketArt,
            serialArea,
            foil,
            zones,
            gridSize,
            allowNearMiss,
            prizes,
            json);
    }

    private static ScratchPackResourceRef ParseResourceRef(JsonElement element, string label)
    {
        EnsureObject(element, label);
        EnsureOnlyProperties(element, label, "source", "ref");
        var source = RequiredString(element, "source");
        var reference = RequiredString(element, "ref");
        if (source is not "builtin" and not "package")
            throw new InvalidDataException($"{label}.source 只允許 builtin 或 package。");
        if (source == "package")
            ValidateRelativeResourcePath(reference, ".png");
        else if (reference.Contains('/') || reference.Contains('\\'))
            throw new InvalidDataException($"{label}.ref 的 built-in code 不得包含路徑。");
        return new ScratchPackResourceRef(source, reference);
    }

    private static ScratchPackZone ParseZone(JsonElement element, int index)
    {
        var label = $"scratch.zones[{index}]";
        EnsureObject(element, label);
        EnsureOnlyProperties(element, label,
            "id", "x", "y", "width", "height", "shape", "cornerRadius");
        var id = RequiredString(element, "id");
        var x = RequiredInt32(element, "x");
        var y = RequiredInt32(element, "y");
        var width = RequiredInt32(element, "width");
        var height = RequiredInt32(element, "height");
        var shape = RequiredString(element, "shape");
        int? cornerRadius = null;

        if (shape is not "rectangle" and not "roundedRectangle" and not "circle" and not "ellipse")
            throw new InvalidDataException($"{label}.shape 不支援：{shape}");
        if (shape == "roundedRectangle")
        {
            cornerRadius = RequiredInt32(element, "cornerRadius");
            if (cornerRadius < 0 || cornerRadius * 2 > Math.Min(width, height))
                throw new InvalidDataException($"{label}.cornerRadius 無效。");
        }
        else if (element.TryGetProperty("cornerRadius", out _))
        {
            throw new InvalidDataException($"{label} 只有 roundedRectangle 可提供 cornerRadius。");
        }
        if (shape == "circle" && width != height)
            throw new InvalidDataException($"{label} 為 circle 時 width 必須等於 height。");

        var zone = new ScratchPackZone(id, x, y, width, height, shape, cornerRadius);
        ValidateCanvasRect(new ScratchPackRect(x, y, width, height), label);
        return zone;
    }

    private static IReadOnlyList<ScratchPackPrize> ParsePrizes(JsonElement prizesElement, long issueSize, int gridSize)
    {
        var prizes = new List<ScratchPackPrize>();
        var amounts = new HashSet<long>();
        var index = 0;
        foreach (var element in prizesElement.EnumerateArray())
        {
            var label = $"prizes[{index++}]";
            EnsureObject(element, label);
            EnsureOnlyProperties(element, label, "amount", "count");
            var amount = RequiredInt64(element, "amount");
            var count = RequiredInt64(element, "count");
            if (amount <= 0)
                throw new InvalidDataException($"{label}.amount 必須 > 0。");
            if (count < 0)
                throw new InvalidDataException($"{label}.count 必須 >= 0。");
            if (!amounts.Add(amount))
                throw new InvalidDataException($"prizes amount 重複：{amount}");
            prizes.Add(new ScratchPackPrize(amount, count));
        }

        var expectedTierCount = 2 * gridSize + 1;
        if (prizes.Count != expectedTierCount)
            throw new InvalidDataException($"GameType 1 / {gridSize}x{gridSize} 必須精確包含 {expectedTierCount} 個正獎 Tier。");

        var ordered = prizes.OrderBy(p => p.Amount).ToArray();
        var winningCount = ordered.Sum(p => p.Count);
        if (winningCount > issueSize)
            throw new InvalidDataException("Prize count 加總不可超過 issueSize。");
        return ordered;
    }

    private static void ValidateGameType1Zones(IReadOnlyList<ScratchPackZone> zones, int gridSize)
    {
        if (zones.Count != gridSize * gridSize)
            throw new InvalidDataException($"GameType 1 / {gridSize}x{gridSize} 必須有 {gridSize * gridSize} 個 scratch zones。");
        if (zones.Select(z => z.Id).Distinct(StringComparer.Ordinal).Count() != zones.Count)
            throw new InvalidDataException("scratch zone id 不得重複。");

        var first = zones[0];
        if (zones.Any(z => z.Width != first.Width || z.Height != first.Height || z.Shape != first.Shape ||
                           z.CornerRadius != first.CornerRadius))
            throw new InvalidDataException("GameType 1 所有 scratch zones 的尺寸、shape 與 cornerRadius 必須一致。");

        var xs = zones.Select(z => z.X).Distinct().OrderBy(x => x).ToArray();
        var ys = zones.Select(z => z.Y).Distinct().OrderBy(y => y).ToArray();
        if (xs.Length != gridSize || ys.Length != gridSize)
            throw new InvalidDataException("GameType 1 scratch zones 必須形成標準 N×N 對齊網格。");

        var xGaps = xs.Zip(xs.Skip(1), (a, b) => b - a - first.Width).ToArray();
        var yGaps = ys.Zip(ys.Skip(1), (a, b) => b - a - first.Height).ToArray();
        if (xGaps.Any(g => g < 0) || yGaps.Any(g => g < 0) ||
            xGaps.Distinct().Count() > 1 || yGaps.Distinct().Count() > 1)
            throw new InvalidDataException("GameType 1 scratch zones 的水平／垂直間距必須一致且不可重疊。");

        var expected = new List<(int X, int Y)>();
        foreach (var y in ys)
            foreach (var x in xs)
                expected.Add((x, y));
        if (!zones.Select(z => (z.X, z.Y)).SequenceEqual(expected))
            throw new InvalidDataException("GameType 1 scratch.zones 順序必須為 row-major。");
    }

    private static void ValidateTicketResource(ScratchPackResourceRef resource, string gameType, int canvas, string packageRoot)
    {
        if (resource.Source == "builtin")
        {
            if (canvas != 1 || !IsBuiltInTicketSupported(gameType, resource.Ref))
                throw new InvalidDataException($"不支援的 built-in 票面：GameType {gameType} / {resource.Ref}");

            var builtInPath = ResolveBuiltInTicketPath(gameType, resource.Ref);
            var (builtInWidth, builtInHeight) = ReadPngDimensions(builtInPath);
            if (canvas == 1 && (builtInWidth != Canvas1Width || builtInHeight != Canvas1Height))
            {
                throw new InvalidDataException(
                    $"Built-in 票面 {gameType}/{resource.Ref} 必須精確為 {Canvas1Width}x{Canvas1Height}，實際為 {builtInWidth}x{builtInHeight}。");
            }
            return;
        }

        var path = ResolveInside(packageRoot, ValidateRelativeResourcePath(resource.Ref, ".png"));
        var (width, height) = ReadPngDimensions(path);
        if (canvas == 1 && (width != Canvas1Width || height != Canvas1Height))
            throw new InvalidDataException($"票面 PNG 必須精確為 {Canvas1Width}x{Canvas1Height}，實際為 {width}x{height}。");
    }

    private static void ValidateFoilResource(ScratchPackResourceRef resource, string packageRoot)
    {
        if (resource.Source == "builtin")
        {
            if (!BuiltInFoils.Contains(resource.Ref))
                throw new InvalidDataException($"不支援的 built-in 銀膜：{resource.Ref}");
            _ = ReadPngDimensions(ResolveBuiltInFoilPath(resource.Ref));
            return;
        }

        var path = ResolveInside(packageRoot, ValidateRelativeResourcePath(resource.Ref, ".png"));
        _ = ReadPngDimensions(path);
    }

    private static ScratchPackRect ParseRect(JsonElement element, string label)
    {
        EnsureObject(element, label);
        EnsureOnlyProperties(element, label, "x", "y", "width", "height");
        return new ScratchPackRect(
            RequiredInt32(element, "x"),
            RequiredInt32(element, "y"),
            RequiredInt32(element, "width"),
            RequiredInt32(element, "height"));
    }

    private static void ValidateCanvasRect(ScratchPackRect rect, string label)
    {
        if (rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0 ||
            rect.X + rect.Width > Canvas1Width || rect.Y + rect.Height > Canvas1Height)
            throw new InvalidDataException($"{label} 超出 Canvas 或尺寸無效。");
    }

    private static (int Width, int Height) ReadPngDimensions(string path)
    {
        if (!File.Exists(path))
            throw new InvalidDataException($"找不到 PNG 資源：{Path.GetFileName(path)}");
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[24];
        stream.ReadExactly(header);
        ReadOnlySpan<byte> signature = stackalloc byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (!header[..8].SequenceEqual(signature) ||
            header[12] != (byte)'I' || header[13] != (byte)'H' || header[14] != (byte)'D' || header[15] != (byte)'R')
            throw new InvalidDataException($"資源不是有效 PNG：{Path.GetFileName(path)}");
        return (
            BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4)));
    }

    private static void ValidateMinimumAppVersion(Version minimum)
    {
        var current = typeof(ScratchPackV1Loader).Assembly.GetName().Version ?? new Version(0, 0, 0);
        if (minimum > current)
            throw new InvalidDataException($"此 ScratchPack 需要 ScratchGame {minimum} 以上版本；目前為 {current.Major}.{current.Minor}.{current.Build}。");
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static JsonDocument ParseJson(string json, string label)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON 格式錯誤：{label}", ex);
        }
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

    private static string RequiredStringAllowEmpty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"缺少或無效的字串欄位：{name}");
        return property.GetString() ?? string.Empty;
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

    private static string ValidateRelativeResourcePath(string path, string requiredExtension)
    {
        path = path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains(':', StringComparison.Ordinal) ||
            path.Split('/').Any(part => part is ".." or "." or ""))
            throw new InvalidDataException($"不合法的相對路徑：{path}");
        if (!string.Equals(Path.GetExtension(path), requiredExtension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"資源必須是 {requiredExtension}：{path}");
        return path;
    }

    public static string ResolveInside(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"資源路徑逃出 ScratchPack：{relative}");
        return fullPath;
    }
}
