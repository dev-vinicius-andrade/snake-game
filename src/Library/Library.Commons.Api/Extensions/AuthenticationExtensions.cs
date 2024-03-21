using Library.Commons.Api.Attributes.Filters;
using Library.Commons.Api.Authentication;
using Library.Commons.Api.Entities.Configurations;
using Library.Commons.Api.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Commons.Api.Extensions;

public static class AuthenticationExtensions
{
    public static void AddApiKeyAuth(this IServiceCollection services, IConfiguration configuration,string sectionName= AuthConfiguration.SectionName)
    {
        services.Configure<AuthConfiguration>(configuration.GetSection(sectionName));
        services.AddScoped<ApiKeyAuth>();
        services.AddScoped<IApiKeyValidatorService, ApiKeyValidatorService>();
    }
}