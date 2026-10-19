namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IRoom : ITrackableId
{
    int Width { get; set; }
    int Height { get; set; }
    string? Name { get; set; }

    /// <summary>The invite code, upper case, or null for rooms nobody can join by code.</summary>
    string? Code { get; set; }

    bool IsPrivate { get; set; }

    /// <summary>The most players the host allows, or zero for the server default.</summary>
    int MaxPlayers { get; set; }

    DateTimeOffset CreatedAt { get; set; }

    /// <summary>What this room does differently from the default gameplay. Replaced as a whole when it changes.</summary>
    Library.Commons.Game.Domain.Entities.RoomGameplay Gameplay { get; set; }
}