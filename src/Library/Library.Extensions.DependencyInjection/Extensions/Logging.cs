using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Library.Extensions.DependencyInjection.Extensions;

public static class Logging
{
    public static ILoggingBuilder ConfigureLogger(this IHostBuilder hostBuilder, ILoggingBuilder loggerBuilder)

    {
        loggerBuilder.ClearProviders();
        hostBuilder.UseSerilog();
        return loggerBuilder;
    }
}