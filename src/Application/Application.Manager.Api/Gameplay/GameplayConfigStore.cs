using System.Text.Json;
using Domain.Game.Rules;

namespace Application.Manager.Api.Gameplay;

/// <summary>
/// The gameplay config the game servers run on. It lives in memory and, when a path is given, in a file, so it survives a
/// restart of the manager. Without a file, a restart goes back to the defaults.
/// </summary>
public sealed class GameplayConfigStore
{
    private readonly string? _path;
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private volatile GameplayDocument _current;

    public GameplayConfigStore(string? path = null, ILogger? logger = null)
    {
        _path = string.IsNullOrWhiteSpace(path) ? null : path;
        _logger = logger;
        _current = Load() ?? new GameplayDocument(NewRevision(), GameplayConfig.Defaults);
    }

    public GameplayDocument Current => _current;

    /// <summary>Saves a config that holds up and makes it the current one. Throws, and changes nothing, when it does not.</summary>
    public GameplayDocument Update(GameplayConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var complete = config.WithBuiltInPerks();
        complete.EnsureValid();
        lock (_lock)
        {
            var document = new GameplayDocument(NewRevision(), complete);
            Save(document);
            _current = document;
            return document;
        }
    }

    private static string NewRevision() => Guid.NewGuid().ToString("N");

    private GameplayDocument? Load()
    {
        if (_path is null || !File.Exists(_path)) return null;
        try
        {
            var document = JsonSerializer.Deserialize<GameplayDocument>(File.ReadAllText(_path), GameplayConfig.Json);
            if (document is null || string.IsNullOrEmpty(document.Revision)) return null;
            var config = document.Config.WithBuiltInPerks();
            config.EnsureValid();
            return document with { Config = config };
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            _logger?.LogWarning(exception, "The saved gameplay config at {Path} could not be used; starting from the defaults", _path);
            return null;
        }
    }

    private void Save(GameplayDocument document)
    {
        if (_path is null) return;
        try
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(_path));
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(_path, JsonSerializer.Serialize(document, GameplayConfig.Json));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(exception, "The gameplay config could not be saved to {Path}; it is kept in memory only", _path);
        }
    }
}
