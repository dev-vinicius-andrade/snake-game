using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Entities;

public sealed record RoomSnapshot(IRoom Room, int Players);
