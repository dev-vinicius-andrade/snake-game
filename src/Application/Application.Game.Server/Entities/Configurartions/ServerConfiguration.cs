using System.Text.Json.Serialization;
using Library.Commons.Game.Domain.Entities.Configurations;

namespace Application.Game.Server.Entities.Configurartions;

public class ServerConfiguration
{
    [JsonPropertyName("roomsConfiguration")]
    public RoomsConfiguration RoomsConfiguration { get; set; }
}