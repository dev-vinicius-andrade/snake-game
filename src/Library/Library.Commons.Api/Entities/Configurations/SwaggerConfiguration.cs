using Microsoft.OpenApi.Models;

namespace Library.Commons.Api.Entities.Configurations;

public class SwaggerConfiguration
{
    public const string SectionName = nameof(SwaggerConfiguration);
    public string Title { get; set; }
    public string Name { get; set; }
    public string Endpoint { get; set; } = "/swagger/v1/swagger.json";
    public string Version { get; set; } = "v1";
    public string Description { get; set; }

    public string Project { get; set; } = string.Empty;

    public List<string> Servers { get; set; } = new();
    public OpenApiSecurityScheme? AuthenticationConfiguration { get; set; }
}