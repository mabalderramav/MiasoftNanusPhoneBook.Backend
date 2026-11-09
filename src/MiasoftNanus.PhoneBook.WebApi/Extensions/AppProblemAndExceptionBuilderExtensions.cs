using MiasoftNanus.PhoneBook.WebApi.ExceptionHandlers;

namespace MiasoftNanus.PhoneBook.WebApi.Extensions;

/// <summary>
/// Provides extension methods for registering and configuring application Problem Details and Exception services.
/// </summary>
public static class AppProblemAndExceptionBuilderExtensions
{
    /// <summary>
    /// Registers Problem Details support and the global exception handler for the application.
    /// </summary>
    /// <param name="services">The service collection to which Problem Details and exception handling
    /// services are added.</param>
    public static void AddProblemDetailsAndExceptionsBuilderExtensions(
        this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
    }
}