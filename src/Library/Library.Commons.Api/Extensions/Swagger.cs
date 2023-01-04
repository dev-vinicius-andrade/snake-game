using Library.Commons.Api.Attributes.Filters;
using Library.Commons.Api.Entities.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Library.Commons.Api.Extensions;

public static class Swagger
{
    public static void AddSwaggerDocumentation(this IServiceCollection services, SwaggerConfiguration swaggerConfig)
    {
        services.AddSwaggerGen(options =>
        {
            options.OperationFilter<SwaggerOptionalRouteParameterOperationFilter>();
            options.SwaggerDoc(swaggerConfig.Version, new OpenApiInfo
            {
                Title = swaggerConfig.Title,
                Version = swaggerConfig.Version,
                Description = swaggerConfig.Description
            });

            if (swaggerConfig.Servers.Count > 0)
                foreach (var sever in swaggerConfig.Servers)
                    options.AddServer(new OpenApiServer { Url = sever });

            if (!string.IsNullOrEmpty(swaggerConfig.Project))
            {
                var file = Path.Combine(AppContext.BaseDirectory, $"{swaggerConfig.Project}.xml");
                options.IncludeXmlComments(file);
            }

            if (swaggerConfig.AuthenticationConfiguration == null) return;

            options.AddSecurityDefinition(swaggerConfig.AuthenticationConfiguration.Name,
                swaggerConfig.AuthenticationConfiguration);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { { swaggerConfig.AuthenticationConfiguration, new List<string>() } });
        });
    }
    public static void UseSwaggerDocumentation(this IApplicationBuilder app, SwaggerConfiguration swaggerConfig)
    {
        app.UseSwagger().UseSwaggerUI(options => { options.SwaggerEndpoint(swaggerConfig.Endpoint, swaggerConfig.Name); });
    }
}