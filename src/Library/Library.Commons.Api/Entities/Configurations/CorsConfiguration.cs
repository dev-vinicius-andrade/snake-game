using Library.Commons.Api.Contants;

namespace Library.Commons.Api.Entities.Configurations;

public class CorsConfiguration
{
    public bool AllowAll { get; set; } = true;
    public string PolicyName { get; set; } = CorsPolicyNames.DefaultCorsPolicy;
    public List<string> AllowedMethods { get; set; } = new();
    public List<string> AllowedOrigins { get; set; } = new();
    public List<string> AllowedCredentials { get; set; } = new();
    public List<string> AllowedHeaders { get; set; } = new();
}