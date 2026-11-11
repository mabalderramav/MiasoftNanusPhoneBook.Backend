using MiasoftNanus.PhoneBook.Domain.WorkAreas.Services;
using MiasoftNanus.PhoneBook.WebApi.WorkAreas.Dtos;
using MiasoftNanus.PhoneBook.WebApi.WorkAreas.Mappers;
using MiasoftNanus.PhoneBook.WebApi.Shared;

namespace MiasoftNanus.PhoneBook.WebApi.WorkAreas.Endpoints;

/// <summary>
/// Provides extension methods for mapping work-area-related endpoints.
/// </summary>
public static class WorkAreaEndpoints
{
    public static void MapWorkAreaEndpoints(this IEndpointRouteBuilder app)
    {
        var workAreaGroup = app.MapGroup("/work-areas");
        workAreaGroup.MapGet("/", GetWorkAreaAll);
        workAreaGroup.MapGet("/{id:guid}", GetWorkAreaById);
        workAreaGroup.MapPost("/", CreateWorkArea);
        workAreaGroup.MapPut("/{id:guid}", UpdateWorkArea);
        workAreaGroup.MapDelete("/{id:guid}", DeleteWorkArea);
    }

    private static async Task<IResult> GetWorkAreaAll(
        IWorkAreaService workAreaService,
        CancellationToken cancellationToken)
    {
        var result = await workAreaService.GetAllAsync(cancellationToken);
        if (result.IsFailure)
            return TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result));

        return TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> GetWorkAreaById(
        Guid id,
        IWorkAreaService workAreaService,
        CancellationToken cancellationToken)
    {
        var result = await workAreaService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result));

        return TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> CreateWorkArea(
        CreateUpdateWorkAreaDto createUpdateWorkAreaDto,
        IWorkAreaService workAreaService,
        CancellationToken cancellationToken)
    {
        var workAreaResult = createUpdateWorkAreaDto.ToEntity();
        if (workAreaResult.IsFailure)
            return TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(workAreaResult));

        var result = await workAreaService.CreateAsync(workAreaResult.Value, cancellationToken);
        return result.IsFailure
            ? TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(result))
            : TypedResults.Created($"/work-areas/{result.Value.Id}", result.Value.ToDto());
    }

    private static async Task<IResult> UpdateWorkArea(
        Guid id,
        CreateUpdateWorkAreaDto createUpdateWorkAreaDto,
        IWorkAreaService workAreaService,
        CancellationToken cancellationToken)
    {
        var workAreaResult = createUpdateWorkAreaDto.ToEntity();
        if (workAreaResult.IsFailure)
            return TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(workAreaResult));

        var result = await workAreaService.UpdateAsync(id, workAreaResult.Value, cancellationToken);
        return result.IsFailure
            ? TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result))
            : TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> DeleteWorkArea(
        Guid id,
        IWorkAreaService workAreaService,
        CancellationToken cancellationToken)
    {
        var result = await workAreaService.DeleteAsync(id, cancellationToken);
        return result.IsFailure
            ? TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result))
            : TypedResults.NoContent();
    }
}
