using Microsoft.Extensions.DependencyInjection;

namespace Library.Extensions.DependencyInjection.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class AutoImportAttribute : Attribute
{
    public AutoImportAttribute(ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        ServiceLifetime = serviceLifetime;
    }

    public ServiceLifetime ServiceLifetime { get; set; }
}