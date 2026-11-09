using MiasoftNanus.PhoneBook.WebApi.Config;
using Microsoft.Extensions.Options;

namespace MiasoftNanus.PhoneBook.WebApi.Healths.Endpoints;

/// <summary>
/// Provides a collection of endpoints related to health check functionalities within the API.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var version = typeof(HealthEndpoints).Assembly.GetName().Version?.ToString() ?? "unknown";


        app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
        app.MapGet("/version", () => Results.Ok(new { version }));

        app.MapGet("/info", () => Results.Ok(new
        {
            name = "Miasoft Nanus PhoneBook API",
            version,

            description = "A simple phone book API built with ASP.NET Core."
        }));
        app.MapGet("/status", () => Results.Ok(new { status = "Running", timestamp = DateTime.UtcNow }));
        app.MapGet("/metrics", (IHostEnvironment env) =>
        {
            if (!env.IsDevelopment())
            {
                return Results.NotFound();
            }

            return Results.Ok(new
            {
                uptime = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime())
                    .TotalSeconds,
                memoryUsage = GC.GetTotalMemory(false),
                threadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count
            });
        });
        app.MapGet("/config",
            (IConfiguration configuration, IOptions<ApiConfig> apiConfigOptions, IHostEnvironment env) =>

            {
                if (!env.IsDevelopment())

                {
                    return Results.NotFound();
                }
                
                var apiConfig = apiConfigOptions.Value;
                var environment = configuration["ASPNETCORE_ENVIRONMENT"];
                return Results.Ok(new
                {
                    environment,
                    timeout = apiConfig.Timeout,
                    baseUrl = apiConfig.BaseUrl
                });
            });
    }
}