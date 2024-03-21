using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Library.Commons.Api.Contants;
using Library.Commons.Game.Domain.Interfaces.Services;
using System.Net.Http;
using Library.Commons.Entities.Configurations;
using Library.Commons.Game.Server.Services;


namespace Library.Commons.Game.Server.Extensions;

public static class GameDependencyInjection
{
    public static IServiceCollection AddPlayers(this IServiceCollection services)
    
    {
        services.Add<ConcurrentDictionary<Guid, IPlayer>, ConcurrentDictionary<Guid, IPlayer>>(ServiceLifetime.Singleton);
        return services;
    }
    public static IServiceCollection AddRooms(this IServiceCollection services)
    {
        services.Add<ConcurrentDictionary<Guid, IRoom>, ConcurrentDictionary<Guid, IRoom>>(ServiceLifetime.Singleton);
        return services;
    }    public static IServiceCollection AddGameState(this IServiceCollection services)
        
    {
        services.Add<ConcurrentDictionary<Guid, IGameState>>(ServiceLifetime.Singleton);
        return services;
    }

    public static IServiceCollection AddPlayerRooms(this IServiceCollection services)
    {
        services.Add<ConcurrentDictionary<IRoom, IEnumerable<IPlayer>>, ConcurrentDictionary<IRoom, IEnumerable<IPlayer>>>(ServiceLifetime.Singleton);
        return services;
    }
    public static IServiceCollection AddSnakeConfiguration(this IServiceCollection services,IConfiguration configuration,string sectionName=SnakeConfiguration.SectionName)
    {
        services.Configure<SnakeConfiguration>(configuration.GetSection(sectionName));
        return services;
    }
    public static IServiceCollection AddFoodConfiguration(this IServiceCollection services, IConfiguration configuration, string sectionName = SnakeConfiguration.SectionName)
    {
        services.Configure<SnakeConfiguration>(configuration.GetSection(sectionName));
        return services;
    }
    public static IServiceCollection AddServerConfiguration(this IServiceCollection services, IConfiguration configuration, string sectionName = ServerConfiguration.SectionName)
    {
        services.Configure<ServerConfiguration>(configuration.GetSection(sectionName));
        return services;
    }



    public static IServiceCollection AddManagementApi(this IServiceCollection services,ApiIntegrationConfiguration apiConfiguration)
    {
        services.AddHttpClient<IManagementApiService, ManagementApiService>(client =>
        {

            client.BaseAddress = new Uri(apiConfiguration.BaseUrl);
            client.DefaultRequestHeaders.Add(AuthConstants.ApiKeyHeaderName,
                apiConfiguration.ApiKey);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback =
                (httpRequestMessage, cert, cetChain, policyErrors) => true
        });

        return services;
    }

}