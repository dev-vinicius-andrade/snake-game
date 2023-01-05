using Library.Commons.Api.Entities;
using Library.Commons.Game.Server.Constants;
using Microsoft.AspNetCore.Routing;

namespace Library.Commons.Game.Server.Extensions;

public static class EndpointExtensions
{
    public static CorsPolicyName CreateHubEndPoint(this IEndpointRouteBuilder endpoint, string? hubEndpoint=HubEndpoints.Game)
    => hubEndpoint != null && hubEndpoint.StartsWith("/") ? $"{hubEndpoint}" : $"/{hubEndpoint}";


}