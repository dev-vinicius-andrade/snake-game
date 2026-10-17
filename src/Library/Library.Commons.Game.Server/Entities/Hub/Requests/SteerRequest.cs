namespace Library.Commons.Game.Server.Entities.Hub.Requests;

/// <summary>Angle is the wanted heading in radians: 0 is right, positive turns clockwise on screen (up is -PI/2).</summary>
public record SteerRequest(double Angle);
