using Library.Commons.Api.Contants;
using Library.Commons.Api.Entities.Configurations;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Library.Commons.Api.Attributes.Filters;

public class SwaggerAuthApiKeyOperationFilter : IOperationFilter
{

    private readonly IOptions<SwaggerConfiguration> _swaggerConfiguration;

    public SwaggerAuthApiKeyOperationFilter(IOptions<SwaggerConfiguration> swaggerConfiguration)
    {
        _swaggerConfiguration = swaggerConfiguration;
    }

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var filterDescriptor = context.ApiDescription.ActionDescriptor.FilterDescriptors;
        var isApiKeyValidator = filterDescriptor.Select(filterInfo => filterInfo.Filter).Any(filter => filter is ApiKeyAuth);
        
        if (!isApiKeyValidator) return;
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = AuthConstants.ApiKeyHeaderName,
            In = ParameterLocation.Header,
            Description = "Api Key",
            Required = true,
            
        });
        operation.Responses.Add("401", new OpenApiResponse { Description = "Unauthorized" });
        operation.Responses.Add("403", new OpenApiResponse { Description = "Forbidden" });

        operation.Security = new List<OpenApiSecurityRequirement>
        {
            new()
            {{
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = AuthConstants.ApiKeyHeaderName
                        },
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.ApiKey,

                    }, new List<string>()}
            }
        };

    }
}