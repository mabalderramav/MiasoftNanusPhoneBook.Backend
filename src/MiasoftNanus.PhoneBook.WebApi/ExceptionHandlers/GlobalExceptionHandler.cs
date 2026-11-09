using Microsoft.AspNetCore.Diagnostics;

namespace MiasoftNanus.PhoneBook.WebApi.ExceptionHandlers;

/// <summary>
/// Handles unhandled exceptions for the web API by logging the error and writing a standardized
/// Problem Details response.
/// </summary>
/// <param name="problemDetailsService">The service used to write Problem Details responses.</param>
/// <param name="logger">The logger used to record unhandled exception details.</param>
/// <remarks>
/// Sets the HTTP response status code to 500 and returns a generic error message to avoid exposing
/// implementation details.
/// </remarks>
public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Exception doesn't handled {Metodo} {Ruta}",
            httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Title = "An unexpected error occurred",
                Detail = "The server could not process the request. Please try again later.",
                Status = StatusCodes.Status500InternalServerError
            }
        });
    }
}