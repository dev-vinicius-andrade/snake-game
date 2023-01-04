using Microsoft.AspNetCore.Mvc.Routing;

namespace Library.Commons.Api.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class ApiContextNameDefinitionAttribute : Attribute, IRouteValueProvider
{
    public ApiContextNameDefinitionAttribute(string contextDefinitionName)
    {
        ContextDefinitionName = contextDefinitionName;
        RouteKey = "contextName";
        RouteValue = contextDefinitionName;
    }

    public string ContextDefinitionName { get; }
    public string RouteKey { get; }
    public string RouteValue { get; }
}