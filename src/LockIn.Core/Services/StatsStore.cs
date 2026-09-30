using System.Text.Json;
using LockIn.Core.Models;

namespace LockIn.Core.Services;

public sealed class StatsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _statsPath;

    public StatsStore(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        _statsPath = Path.Combine(rootDirectory, "stats.json");
    }

    public UserStats Load()
    {
        if (!File.Exists(_statsPath))
        {
            return new UserStats();
        }

        try
        {
            var stats = JsonSerializer.Deserialize<UserStats>(File.ReadAllText(_statsPath), JsonOptions)
                        ?? new UserStats();
            stats.CompletedSessionIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return stats;
        }
        catch (JsonException)
        {
            return new UserStats();
        }
    }

    public async Task<bool> MarkCompletedAsync(
        FocusSession session,
        DateTimeOffset completedAtLocal,
        CancellationToken cancellationToken = default)
    {
        var stats = Load();
        if (!stats.ApplyCompletedSession(session, completedAtLocal))
        {
            return false;
        }

        var tempPath = _statsPath + ".tmp";
        var json = JsonSerializer.Serialize(stats, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, _statsPath, overwrite: true);
        return true;
    }
}
