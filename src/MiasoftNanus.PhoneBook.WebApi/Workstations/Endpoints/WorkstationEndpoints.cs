using MiasoftNanus.PhoneBook.Domain.Workstations.Services;
using MiasoftNanus.PhoneBook.WebApi.Workstations.Dtos;
using MiasoftNanus.PhoneBook.WebApi.Workstations.Mappers;
using MiasoftNanus.PhoneBook.WebApi.Shared;

namespace MiasoftNanus.PhoneBook.WebApi.Workstations.Endpoints;

/// <summary>
/// Provides extension methods for mapping workstation-related endpoints.
/// </summary>
public static class WorkstationEndpoints
{
    public static void MapWorkstationEndpoints(this IEndpointRouteBuilder app)
    {
        var workstationGroup = app.MapGroup("/workstations");
        workstationGroup.MapGet("/", GetWorkstationAll)
            .ProducesProblem(StatusCodes.Status404NotFound);
        workstationGroup.MapGet($"/{{id:guid}}", GetWorkstationById)
            .ProducesProblem(StatusCodes.Status404NotFound);
        workstationGroup.MapPost("/", CreateWorkstation)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        workstationGroup.MapPut($"/{{id:guid}}", UpdateWorkstation)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        workstationGroup.MapDelete($"/{{id:guid}}", DeleteWorkstation)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetWorkstationAll(
        IWorkstationService workstationService,
        CancellationToken cancellationToken)
    {
        var result = await workstationService.GetAllAsync(cancellationToken);
        if (result.IsFailure)
            return TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result));

        return TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> GetWorkstationById(
        Guid id,
        IWorkstationService workstationService,
        CancellationToken cancellationToken)
    {
        var result = await workstationService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result));

        return TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> CreateWorkstation(
        CreateUpdateWorkstationDto createUpdateWorkstationDto,
        IWorkstationService workstationService,
        CancellationToken cancellationToken)
    {
        var workstationResult = createUpdateWorkstationDto.ToEntity();
        if (workstationResult.IsFailure)
            return TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(workstationResult));

        var result = await workstationService.CreateAsync(workstationResult.Value, cancellationToken);
        return result.IsFailure
            ? TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(result))
            : TypedResults.Created($"/workstations/{result.Value.Id}", result.Value.ToDto());
    }

    private static async Task<IResult> UpdateWorkstation(
        Guid id,
        CreateUpdateWorkstationDto createUpdateWorkstationDto,
        IWorkstationService workstationService,
        CancellationToken cancellationToken)
    {
        var workstationResult = createUpdateWorkstationDto.ToEntity();
        if (workstationResult.IsFailure)
            return TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(workstationResult));

        var result = await workstationService.UpdateAsync(id, workstationResult.Value, cancellationToken);
        return result.IsFailure
            ? TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result))
            : TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> DeleteWorkstation(
        Guid id,
        IWorkstationService workstationService,
        CancellationToken cancellationToken)
    {
        var result = await workstationService.DeleteAsync(id, cancellationToken);
        return result.IsFailure
            ? TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result))
            : TypedResults.NoContent();
    }
}
