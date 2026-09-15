using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ScratchGame.Models;

namespace PackEditor;

internal sealed class PackDraft
{
    public Guid PackageId { get; private set; }
    public string Name { get; set; } = "未命名彩券";
    public string Author { get; set; } = string.Empty;
    public string GameType { get; set; } = "1";
    public long Price { get; set; } = 500;
    public int Canvas { get; set; } = 1;
    public Version MinimumAppVersion { get; set; } = new(0, 0, 0);

    public EditorResourceSelection TicketArt { get; } = new();
    public EditorResourceSelection Foil { get; } = new();

    public int GridSize { get; set; } = 3;
    public bool AllowNearMiss { get; set; }
    public int ZoneX { get; set; } = 250;
    public int ZoneY { get; set; } = 220;
    public int ZoneWidth { get; set; } = 150;
    public int ZoneHeight { get; set; } = 100;
    public int HorizontalGap { get; set; } = 20;
    public int VerticalGap { get; set; } = 20;
    public string ZoneShape { get; set; } = "roundedRectangle";
    public int CornerRadius { get; set; } = 12;

    public bool PriceDisplay { get; set; } = true;
    public EditorRect PriceArea { get; } = new(840, 42, 200, 82);
    public EditorRect SerialArea { get; } = new(365, 804, 350, 50);

    public long IssueSize { get; set; } = 1000;
    public long TicketsPerBook { get; set; } = 100;
    public ObservableCollection<EditorPrizeTier> Prizes { get; } = new();

    public static PackDraft CreateNew(Version currentEditorVersion)
    {
        var draft = new PackDraft
        {
            PackageId = Guid.NewGuid(),
            MinimumAppVersion = NormalizeThreePartVersion(currentEditorVersion)
        };
        draft.EnsurePrizeTiers();
        return draft;
    }

    public void RegeneratePackageId()
        => PackageId = Guid.NewGuid();

    public void EnsurePrizeTiers()
    {
        var lineCounts = GetLegalPositiveLineCounts(GridSize);
        while (Prizes.Count > lineCounts.Count)
            Prizes.RemoveAt(Prizes.Count - 1);

        while (Prizes.Count < lineCounts.Count)
        {
            var previousAmount = Prizes.Count == 0 ? 0 : Prizes[^1].Amount;
            var increment = Math.Max(1, Price);
            Prizes.Add(new EditorPrizeTier(lineCounts[Prizes.Count], previousAmount + increment, 0));
        }

        for (var i = 0; i < lineCounts.Count; i++)
            Prizes[i].LineCount = lineCounts[i];
    }

    public IReadOnlyList<ScratchPackZone> BuildZones()
    {
        var zones = new List<ScratchPackZone>(GridSize * GridSize);
        var index = 1;
        for (var row = 0; row < GridSize; row++)
        {
            for (var column = 0; column < GridSize; column++)
            {
                var x = ZoneX + column * (ZoneWidth + HorizontalGap);
                var y = ZoneY + row * (ZoneHeight + VerticalGap);
                zones.Add(new ScratchPackZone(
                    $"cell{index++:D2}",
                    x,
                    y,
                    ZoneWidth,
                    ZoneHeight,
                    ZoneShape,
                    ZoneShape == "roundedRectangle" ? CornerRadius : null));
            }
        }
        return zones;
    }

    public IReadOnlyList<ScratchPackPrize> BuildPrizes()
        => Prizes.Select(prize => new ScratchPackPrize(prize.Amount, prize.Count)).ToArray();

    public static IReadOnlyList<int> GetLegalPositiveLineCounts(int gridSize)
    {
        var result = Enumerable.Range(1, 2 * gridSize).ToList();
        result.Add(2 * gridSize + 2);
        return result;
    }

    private static Version NormalizeThreePartVersion(Version version)
        => new(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build));
}

internal sealed class EditorResourceSelection
{
    public string Source { get; set; } = "builtin";
    public string Reference { get; set; } = string.Empty;
    public string? ExternalPath { get; set; }

    public ScratchPackResourceRef ToResourceRef()
        => new(Source, Reference);
}

internal sealed class EditorRect
{
    public EditorRect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public ScratchPackRect ToRect() => new(X, Y, Width, Height);
}

internal sealed class EditorPrizeTier : INotifyPropertyChanged
{
    private int _lineCount;
    private long _amount;
    private long _count;

    public EditorPrizeTier(int lineCount, long amount, long count)
    {
        _lineCount = lineCount;
        _amount = amount;
        _count = count;
    }

    public int LineCount
    {
        get => _lineCount;
        set { if (_lineCount == value) return; _lineCount = value; OnPropertyChanged(); }
    }

    public long Amount
    {
        get => _amount;
        set { if (_amount == value) return; _amount = value; OnPropertyChanged(); }
    }

    public long Count
    {
        get => _count;
        set { if (_count == value) return; _count = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
