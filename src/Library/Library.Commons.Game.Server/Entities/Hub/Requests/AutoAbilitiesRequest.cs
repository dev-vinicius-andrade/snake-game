namespace Library.Commons.Game.Server.Entities.Hub.Requests;

/// <summary>Active asks the server to use the ready cooldown perks by itself when they would help, for controls that have no key for them.</summary>
public record AutoAbilitiesRequest(bool Active);
