using System.Text.Json.Serialization;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;

[Serializable]
internal record Score():IScore
{

    [JsonPropertyName("currentScore")]
    public long CurrentScore { get; set; }=0;
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = null!;
}