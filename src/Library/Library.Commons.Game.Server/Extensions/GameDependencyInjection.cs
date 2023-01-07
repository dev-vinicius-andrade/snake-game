using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;


namespace Library.Commons.Game.Server.Extensions;

public static class GameDependencyInjection
{
    public static IServiceCollection AddPlayers<T>(this IServiceCollection services)
    where T : class, IPlayer
    {
        services.Add<ConcurrentDictionary<Guid, T>, ConcurrentDictionary<Guid, T>>(ServiceLifetime.Singleton);
        return services;
    }
    public static IServiceCollection AddRooms<T>(this IServiceCollection services)
        where T : class, IRoom
    {
        services.Add<ConcurrentDictionary<Guid, T>, ConcurrentDictionary<Guid, T>>(ServiceLifetime.Singleton);
        return services;
    }
    public static IServiceCollection AddPlayerRooms<TRoom,TPlayer>(this IServiceCollection services)
        where TRoom : class, IRoom
        where TPlayer : class, IPlayer
    {
        services.Add<ConcurrentDictionary<TRoom, IList<TPlayer>>, ConcurrentDictionary<TRoom, IList<TPlayer>>>(ServiceLifetime.Singleton);
        return services;
    }
}