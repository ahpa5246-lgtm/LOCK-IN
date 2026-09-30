using System.Text.Json;
using LockIn.Core.Models;

namespace LockIn.Core.Services;

public sealed class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _sessionPath;

    public SessionStore(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        _sessionPath = Path.Combine(rootDirectory, "active-session.json");
    }

    public FocusSession? LoadActive()
    {
        if (!File.Exists(_sessionPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_sessionPath);
            var session = JsonSerializer.Deserialize<FocusSession>(json, JsonOptions);
            session?.Validate();

            if (session is null || !session.IsActive(DateTimeOffset.UtcNow))
            {
                Clear();
                return null;
            }

            return session;
        }
        catch (JsonException)
        {
            QuarantineCorruptState();
            return null;
        }
        catch (InvalidOperationException)
        {
            QuarantineCorruptState();
            return null;
        }
    }

    public async Task SaveAsync(FocusSession session, CancellationToken cancellationToken = default)
    {
        session.Validate();
        var json = JsonSerializer.Serialize(session, JsonOptions);
        var tempPath = _sessionPath + ".tmp";

        await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, _sessionPath, overwrite: true);
    }

    public void Clear()
    {
        if (File.Exists(_sessionPath))
        {
            File.Delete(_sessionPath);
        }
    }

    private void QuarantineCorruptState()
    {
        try
        {
            if (File.Exists(_sessionPath))
            {
                File.Move(
                    _sessionPath,
                    _sessionPath + ".corrupt-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    overwrite: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
