using Microsoft.Data.Sqlite;
using ScratchGame.Data;

namespace ScratchGame.Services;

public sealed class BackupService(AppDatabase database)
{
    private static readonly TimeSpan BackupInterval = TimeSpan.FromDays(3);
    private const int MaxBackups = 5;

    public async Task<bool> BackupIfDueAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(database.BackupDirectory);
        var newest = Directory.EnumerateFiles(database.BackupDirectory, "ScratchGame_*.db")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();

        if (newest is not null && DateTime.UtcNow - newest.LastWriteTimeUtc < BackupInterval)
            return false;

        await CreateBackupAsync(cancellationToken);
        return true;
    }

    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(database.BackupDirectory);
        var destinationPath = Path.Combine(
            database.BackupDirectory,
            $"ScratchGame_{DateTime.Now:yyyyMMdd-HHmmss}.db");

        await using var source = await database.OpenConnectionAsync(cancellationToken);
        await using var destination = new SqliteConnection($"Data Source={destinationPath}");
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);

        RotateBackups();
        return destinationPath;
    }

    private void RotateBackups()
    {
        var backups = Directory.EnumerateFiles(database.BackupDirectory, "ScratchGame_*.db")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        foreach (var old in backups.Skip(MaxBackups))
        {
            try
            {
                old.Delete();
            }
            catch (IOException)
            {
                // 備份輪替失敗不應阻止主程式啟動；下次啟動再重試。
            }
            catch (UnauthorizedAccessException)
            {
                // 同上。
            }
        }
    }
}
