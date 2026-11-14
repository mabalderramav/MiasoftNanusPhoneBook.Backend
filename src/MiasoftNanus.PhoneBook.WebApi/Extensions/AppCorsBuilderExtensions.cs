using MiasoftNanus.PhoneBook.WebApi.Shared.Constants;

namespace MiasoftNanus.PhoneBook.WebApi.Extensions;

/// <summary>
/// Provides extension methods for registering application-specific CORS configuration for the PhoneBook Web API.
/// </summary>
public static class AppCorsBuilderExtensions
{
    public static IServiceCollection AddAppCorsService(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName.AllowSpecificOrigin, policyBuilder =>
            {
                policyBuilder.WithOrigins("http://localhost:4200")
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        return services;
    }
}