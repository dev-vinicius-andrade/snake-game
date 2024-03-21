using Library.Commons.Game.Domain.Interfaces.Entities;
using System.Text.Json.Serialization;

namespace Library.Commons.Game.Domain.Entities.Configurations
{
    public sealed class TimeSpanConfiguration: ITimeSpanConfiguration
    {
        [JsonPropertyName("days")]
        public double? Days { get; set; }
        [JsonPropertyName("hours")]
        public double? Hours { get; set; }
        [JsonPropertyName("minutes")]
        public double? Minutes { get; set; }
        [JsonPropertyName("seconds")]
        public double? Seconds { get; set; }
        [JsonPropertyName("milliseconds")]
        public double? Milliseconds { get; set; }
    }
}