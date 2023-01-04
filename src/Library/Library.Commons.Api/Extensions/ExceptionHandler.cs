using Library.Commons.Api.Handlers;
using Library.Commons.Api.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Library.Commons.Api.Extensions;

public static class ExceptionHandler
{
    public static void ConfigureExceptionHandling(this IApplicationBuilder app, IHostEnvironment env)
    {
        if (app.HasRequestExceptionHandler())
            AddDefaultRequestMiddleware(app, env);
        else if (env.IsDevelopment()) app.UseDeveloperExceptionPage();
    }
    public static bool HasRequestExceptionHandler(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var services = scope.ServiceProvider.GetServices<IDefaultRequestExceptionHandler>();
        return services.Any();
    }
    public static void AddDefaultRequestMiddleware(this IApplicationBuilder app, IHostEnvironment env)
    {
        app.UseMiddleware<DefaultRequestMiddleware>();
    }
}