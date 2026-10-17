using System.Security.Cryptography;
using System.Text;
using Library.Commons.Game.Domain.Entities.Eventbus.Payloads;

namespace Application.Manager.Api.Rooms;

/// <summary>Short invite codes that are easy to read out loud: no 0, O, 1, I or L.</summary>
public sealed class InviteCodeGenerator(Random? random = null)
{
    public const int Length = 6;
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int Attempts = 50;
    private readonly Random _random = random ?? Random.Shared;

    public string Generate(Func<string, bool> isTaken)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var code = string.Create(Length, _random, (span, rng) =>
            {
                for (var i = 0; i < span.Length; i++) span[i] = Alphabet[rng.Next(Alphabet.Length)];
            });
            if (!isTaken(code)) return code;
        }
        throw new InvalidOperationException("No free invite code was found.");
    }
}

public static class AdminKeyValidator
{
    /// <summary>True when the key is one of the configured keys. Nothing configured means nobody is an admin.</summary>
    public static bool IsValid(IReadOnlyCollection<string>? configuredKeys, string? provided)
    {
        if (string.IsNullOrEmpty(provided) || configuredKeys is null) return false;
        var bytes = Encoding.UTF8.GetBytes(provided);
        return configuredKeys.Where(key => !string.IsNullOrEmpty(key)).Any(key => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), bytes));
    }
}

public interface IRoomCreatePublisher
{
    void Publish(RoomCreatePayload payload);
}

public sealed record RoomCreationResult(bool Success, string? Code = null, string? Error = null);

/// <summary>Validates what a host asked for, picks an invite code and asks the game servers to make the room.</summary>
public sealed class RoomCreationService(RoomRegistry registry, InviteCodeGenerator codes, IRoomCreatePublisher publisher)
{
    public const int MaxNameLength = 32;
    public const int MaxPlayersLimit = 64;
    private const int MinPlayersWhenSet = 2;

    public RoomCreationResult Request(string? requesterId, string? name, int maxPlayers)
    {
        if (string.IsNullOrWhiteSpace(requesterId)) return new RoomCreationResult(false, Error: "missingConnection");
        var code = codes.Generate(registry.IsCodeTaken);
        try
        {
            publisher.Publish(new RoomCreatePayload { RequesterId = requesterId, Name = CleanName(name), Code = code, MaxPlayers = LimitPlayers(maxPlayers) });
        }
        catch (Exception)
        {
            return new RoomCreationResult(false, Error: "unavailable");
        }
        return new RoomCreationResult(true, code);
    }

    private static string? CleanName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
    }

    private static int LimitPlayers(int requested) => requested <= 0 ? 0 : Math.Clamp(requested, MinPlayersWhenSet, MaxPlayersLimit);
}

public enum CloseResult { Closed, NotFound, Unreachable }

public interface IGameServerClient
{
    Task<CloseResult> CloseRoomAsync(string baseUrl, Guid roomId, CancellationToken cancellationToken = default);
}

public enum DeleteResult { Deleted, NotFound, ServerUnreachable }

/// <summary>What an administrator can do with rooms.</summary>
public sealed class RoomAdminService(RoomRegistry registry, IGameServerClient servers)
{
    public IReadOnlyList<RegisteredRoom> List() => registry.List();

    public async Task<DeleteResult> DeleteAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var room = registry.Get(roomId);
        if (room is null) return DeleteResult.NotFound;
        var result = await servers.CloseRoomAsync(room.BaseUrl, roomId, cancellationToken);
        if (result == CloseResult.Unreachable) return DeleteResult.ServerUnreachable;
        registry.Remove(roomId);
        return DeleteResult.Deleted;
    }
}
