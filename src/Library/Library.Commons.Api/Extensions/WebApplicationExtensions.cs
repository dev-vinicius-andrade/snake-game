using Library.Commons.Api.Entities;
using Library.Extensions.DependencyInjection.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Library.Commons.Api.Extensions;

public static class WebApplicationExtensions
{
    public static void ConfigureDepencyInjection<T>(this WebApplicationBuilder builder, T appSettings, Func<IServiceCollection,IConfiguration ,T,IServiceCollection> configureServices=null)
    where T : BaseAppSettings
    {
        configureServices?.Invoke(builder.Services, builder.Configuration, appSettings);
    }
    public static void Configure<T>(this WebApplication app, T appSettings, CorsPolicyName corsPolicyName, Action<IApplicationBuilder, IHostEnvironment, T, CorsPolicyName> configure = null)
        where T : BaseAppSettings
    {
        configure?.Invoke(app, app.Environment, appSettings, corsPolicyName);
    }
}