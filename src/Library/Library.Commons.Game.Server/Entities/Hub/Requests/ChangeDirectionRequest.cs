using System.Text.Json.Serialization;
using Library.Commons.Game.Domain.Enums;

namespace Library.Commons.Game.Server.Entities.Hub.Requests;

public record ChangeDirectionRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Direction Direction { get; init; }
}