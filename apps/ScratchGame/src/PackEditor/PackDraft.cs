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

    public static PackDraft CreateNew(Version currentEditorVersion)
    {
        return new PackDraft
        {
            PackageId = Guid.NewGuid(),
            MinimumAppVersion = NormalizeThreePartVersion(currentEditorVersion)
        };
    }

    public void RegeneratePackageId()
        => PackageId = Guid.NewGuid();

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
