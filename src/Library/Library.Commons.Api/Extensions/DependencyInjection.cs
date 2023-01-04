using Library.Commons.Api.Contants;
using Library.Commons.Api.Entities;
using Library.Commons.Api.Entities.Configurations;
using Library.Commons.Api.Handlers;
using Library.Commons.Api.Interfaces;
using Library.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Commons.Api.Extensions;

public static class DependencyInjection
{
    public static CorsPolicyName AddAllowAllCorsPolicy(this IServiceCollection services)
    {
        const string corsPolicyName = CorsPolicyNames.AllowAllCorsPolicy;
        var corsPolicyBuilder = new CorsPolicyBuilder();
        var corsPolicy = corsPolicyBuilder.AllowAnyMethod()
            .AllowAnyHeader()
            .SetIsOriginAllowed((host) => true)
            .AllowCredentials()
            .Build();
        services.AddCorsPolicy(corsPolicy, corsPolicyName);
        return corsPolicyName;
    }
    public static CorsPolicyName AddCorsPolicy(this IServiceCollection service, CorsPolicy corsPolicy = null, string corsPolicyName = CorsPolicyNames.DenyAllCorsPolicy)
    {
        var persistedCorsPolicy = corsPolicy ?? new CorsPolicyBuilder().Build();
        service.AddCors(options => options.AddPolicy(corsPolicyName, persistedCorsPolicy));
        return corsPolicyName;
    }
    public static CorsPolicyName AddCorsPolicy(this IServiceCollection service, CorsConfiguration corsConfiguration)
    {
        if (corsConfiguration == null) throw new ArgumentNullException(nameof(corsConfiguration));
        if (corsConfiguration.AllowAll) return AddAllowAllCorsPolicy(service);
        var corsPolicyBuilder = new CorsPolicyBuilder();
        corsPolicyBuilder
            .WithOrigins(corsConfiguration.AllowedOrigins.ToArray())
            .WithHeaders(corsConfiguration.AllowedHeaders.ToArray())
            .WithMethods(corsConfiguration.AllowedMethods.ToArray());
        return corsConfiguration.PolicyName;
    }
    public static void AddRequestExceptionHandler<TIExceptionHandlerService, TExceptionHandlerService>(this IServiceCollection services,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        where TIExceptionHandlerService : class, IRequestExceptionHandler
        where TExceptionHandlerService : class, TIExceptionHandlerService
    {
        services.Add<TIExceptionHandlerService, TExceptionHandlerService>(serviceLifetime);
    }
    public static void AddDefaultRequestExceptionHandler(this IServiceCollection services, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        
        services.Add<DefaultRequestMiddleware>(serviceLifetime);
        services.AddRequestExceptionHandler<IDefaultRequestExceptionHandler, DefaultRequestExceptionHandler>(serviceLifetime);
    }
}