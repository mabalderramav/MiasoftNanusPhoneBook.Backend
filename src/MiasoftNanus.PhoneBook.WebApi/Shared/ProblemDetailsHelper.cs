using MiasoftNanus.PhoneBook.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace MiasoftNanus.PhoneBook.WebApi.Shared;

/// <summary>
/// Provides factory methods for creating ASP.NET Core <see cref="ProblemDetails"/> instances from domain
/// operation results.
/// </summary>
/// <remarks>
/// Maps an operation failure represented by <see cref="Result"/> to a standardized HTTP problem details response,
/// using the result error code as the title and the result error message as the detail.
/// </remarks>
public static class ProblemDetailsHelper
{
    /// <summary>
    /// Creates an ASP.NET Core <see cref="ProblemDetails"/> instance from an operation result.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to assign to the problem details response.</param>
    /// <param name="result">The operation result whose error code and message are used as the problem title
    /// and detail.</param>
    /// <returns>A <see cref="ProblemDetails"/> instance populated with the specified status code and result
    /// error information.</returns>
    private static ProblemDetails CreateProblemDetails(int statusCode, Result result)
    {
        return new ProblemDetails
        {
            Title = result.Error.Code,
            Detail = result.Error.Message,
            Status = statusCode
        };
    }

    /// <summary>
    /// Creates an ASP.NET Core <see cref="ProblemDetails"/> instance representing a not found response from
    /// an operation result.
    /// </summary>
    /// <param name="result">The operation result whose error code and message are used as the problem title
    /// and detail.</param>
    /// <returns>A <see cref="ProblemDetails"/> instance populated with a 404 Not Found status and
    /// the result error information.</returns>
    public static ProblemDetails CreateNotFoundProblemDetails(Result result)
    {
        return CreateProblemDetails(StatusCodes.Status404NotFound, result);
    }

    /// <summary>
    /// Creates an ASP.NET Core <see cref="ProblemDetails"/> instance representing a bad request
    /// from an operation result.
    /// </summary>
    /// <param name="result">The failed operation result whose error code and message are used as the problem title and
    /// detail.</param>
    /// <returns>A <see cref="ProblemDetails"/> instance populated with HTTP 400 Bad Request status and
    /// result error information.</returns>
    public static ProblemDetails CreateBadRequestProblemDetails(Result result)
    {
        return CreateProblemDetails(StatusCodes.Status400BadRequest, result);
    }
}