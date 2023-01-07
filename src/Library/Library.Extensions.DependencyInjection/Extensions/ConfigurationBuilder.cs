using Library.Extensions.DependencyInjection.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Library.Extensions.DependencyInjection.Extensions;

public static class ConfigurationBuilder
{

    public static FolderPath GetCurrentDirectoryBasePath<T>(this T builder, params string[] paths)
        where T : IConfigurationBuilder
    {
        var pathsList = paths ?? new string[] {};
        var persistedPaths = pathsList.Prepend(Directory.GetCurrentDirectory());
        return Path.Combine(persistedPaths.ToArray());
    }
    public static IConfigurationBuilder InitializeAppConfiguration<T>(this T builder, FolderPath basePath = null, IReadOnlyList<JsonConfigurationSource> jsonConfigurationSources = null)
    where T:IConfigurationBuilder

    {
        
        if (!string.IsNullOrEmpty(basePath) && !string.IsNullOrWhiteSpace(basePath))
            builder.SetBasePath(basePath);

        var persistedJsonConfigurationSources = jsonConfigurationSources ?? new List<JsonConfigurationSource> { new() { Optional = true, Path = "appsettings.json", ReloadOnChange = true } };
        foreach (var jsonConfigurationSource in persistedJsonConfigurationSources)
            if (jsonConfigurationSource.Path != null)
                builder.AddJsonFile(jsonConfigurationSource.Path, jsonConfigurationSource.Optional,
                    jsonConfigurationSource.ReloadOnChange);

        builder.AddEnvironmentVariables();
        return builder;
    }

}