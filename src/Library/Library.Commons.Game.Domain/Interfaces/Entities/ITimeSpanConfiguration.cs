namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface ITimeSpanConfiguration
{
    double? Days { get; set; }
    double? Hours { get; set; }
    double? Minutes { get; set; }
    double? Seconds { get; set; }
    double? Milliseconds { get; set; }
}