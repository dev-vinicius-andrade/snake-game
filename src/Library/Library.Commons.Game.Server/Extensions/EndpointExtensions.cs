using System;
using Library.Commons.Api.Entities;
using Microsoft.AspNetCore.Routing;

namespace Library.Commons.Game.Server.Extensions;

public static class EndpointExtensions
{
    public static CorsPolicyName CreateHubEndPoint(this IEndpointRouteBuilder endpoint, string hubEndpoint)
    {
        if (string.IsNullOrWhiteSpace(hubEndpoint))
            throw new ArgumentNullException(nameof(hubEndpoint));
        return hubEndpoint.StartsWith("/") ? $"{hubEndpoint}" : $"/{hubEndpoint}";
    }


}