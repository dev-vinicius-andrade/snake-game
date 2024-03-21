using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Library.Extensions.DependencyInjection.Abstractions;
using Library.Extensions.DependencyInjection.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddJsonSerializeOptions(this IServiceCollection services, Func<JsonSerializerOptions> jsonSerializerOptionsBuilder,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        var options = jsonSerializerOptionsBuilder.Invoke();
        services.Add(options, lifetime);

        return services;
    }
    public static IServiceCollection AddJsonSerializeOptions(this IServiceCollection services, JsonSerializerOptions options,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        services.Add(options, lifetime);

        return services;
    }
    public static IServiceCollection AddDefaultJsonSerializeOptions(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
      services.AddJsonSerializeOptions(new JsonSerializerOptions
      {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          PropertyNameCaseInsensitive = true,
          WriteIndented = true,
          AllowTrailingCommas = true,
          ReadCommentHandling = JsonCommentHandling.Skip,
          DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
          Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
      }, lifetime);

        return services;
    }

    public static TAppSettings AddAppSettings<TAppSettings>(this IServiceCollection services,
        IConfiguration configuration, string sectionName = BaseAppSettings.DefaultSectionName,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    where TAppSettings:BaseAppSettings
    {
        var appSettingsSection = configuration.GetSection(sectionName);
        var appSettings = appSettingsSection.Get<TAppSettings>();
        if (appSettings == null) throw new NullReferenceException(nameof(appSettings));
        services.Configure<TAppSettings>(appSettingsSection);
        return appSettings;
    }
    public static void Add<TService>(
      this IServiceCollection services,
      ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
      where TService : class
    {
      switch (serviceLifetime)
      {
        case ServiceLifetime.Singleton:
          services.AddSingleton<TService>();
          break;
        case ServiceLifetime.Scoped:
          services.AddScoped<TService>();
          break;
        case ServiceLifetime.Transient:
          services.AddTransient<TService>();
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof (serviceLifetime), (object) serviceLifetime, null);
      }
    }
    public static IEnumerable<Type> GetAllAutoImportTypes(this IServiceCollection services, Assembly assembly=null)
    {
        var persistedAssembly  = assembly?? Assembly.GetEntryAssembly();
        return persistedAssembly?.GetTypes().Where(t => t.GetCustomAttribute<AutoImportAttribute>() != null)?.ToList() ?? new List<Type>();
    }
    public static IServiceCollection AddServices(this IServiceCollection services, Assembly assembly = null)
    {
        var existingServicesTypes = services.GetAllAutoImportTypes(assembly);
        foreach (var serviceType in existingServicesTypes)
            AutoImportType(services, serviceType);
        
        return services;
    }

    private static void AutoImportType(this IServiceCollection services, Type serviceType)
    {
        var allInterfaces = serviceType.GetInterfaces();
        var minimalInterfaces = (from iType in allInterfaces
            where !allInterfaces.Any(t => t.GetInterfaces()
                .Contains(iType))
            select iType).ToList();
        foreach (var interfaceType in minimalInterfaces)
        {
            var autoImportAttribute = serviceType.GetCustomAttribute<AutoImportAttribute>();
            if (autoImportAttribute == null) continue;
            services.Add(new ServiceDescriptor(interfaceType, serviceType, autoImportAttribute.ServiceLifetime));
        }
    }

    public static void Add<TService, TImplementation>(
      this IServiceCollection services,
      ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
      where TService : class
      where TImplementation : class, TService
    {
      switch (serviceLifetime)
      {
        case ServiceLifetime.Singleton:
          services.AddSingleton<TService, TImplementation>();
          break;
        case ServiceLifetime.Scoped:
          services.AddScoped<TService, TImplementation>();
          break;
        case ServiceLifetime.Transient:
          services.AddTransient<TService, TImplementation>();
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof (serviceLifetime), (object) serviceLifetime, null);
      }
    }

    public static void Add<TService>(
      this IServiceCollection services,
      TService service,
      ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
      where TService : class
    {
      switch (serviceLifetime)
      {
        case ServiceLifetime.Singleton:
          services.AddSingleton<TService>(service);
          break;
        case ServiceLifetime.Scoped:
          services.AddScoped<TService>((Func<IServiceProvider, TService>) (_ => service));
          break;
        case ServiceLifetime.Transient:
          services.AddTransient<TService>((Func<IServiceProvider, TService>) (_ => service));
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof (serviceLifetime), (object) serviceLifetime, null);
      }
    }
}