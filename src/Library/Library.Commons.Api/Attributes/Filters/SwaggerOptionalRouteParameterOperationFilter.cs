using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Library.Commons.Api.Attributes.Filters;

public class SwaggerOptionalRouteParameterOperationFilter : IOperationFilter
{
    private const string CaptureName = "routeParameter";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var httpMethodAttributes = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<HttpMethodAttribute>();

        var httpMethodWithOptional = httpMethodAttributes
            .FirstOrDefault(methodAttribute => methodAttribute.Template?.Contains("?") ?? false);
        
        if (httpMethodWithOptional?.Template == null)
            return;

        const string regex = $"{{(?<{CaptureName}>\\w+)\\?}}";

        var matches = Regex.Matches(httpMethodWithOptional.Template, regex);

        foreach (Match match in matches)
        {
            var name = match.Groups[CaptureName].Value;

            var parameter = operation.Parameters
                .FirstOrDefault(apiParameter => apiParameter.In == ParameterLocation.Path && apiParameter.Name == name);

            if (parameter == null) continue;

            parameter.AllowEmptyValue = true;
            parameter.Description = "Must check \"Send empty value\" or Swagger passes a comma for empty values otherwise";
            parameter.Required = false;
            parameter.Schema.Nullable = true;
        }
    }
}