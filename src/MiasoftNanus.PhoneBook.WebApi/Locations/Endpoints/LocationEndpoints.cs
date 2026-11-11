using MiasoftNanus.PhoneBook.Domain.Locations.Services;
using MiasoftNanus.PhoneBook.WebApi.Locations.Dtos;
using MiasoftNanus.PhoneBook.WebApi.Locations.Mappers;
using MiasoftNanus.PhoneBook.WebApi.Shared;

namespace MiasoftNanus.PhoneBook.WebApi.Locations.Endpoints;

/// <summary>
/// Provides extension methods for mapping location-related endpoints.
/// </summary>
public static class LocationEndpoints
{
    public static void MapLocationEndpoints(this IEndpointRouteBuilder app)
    {
        var locationGroup = app.MapGroup("/locations");
        locationGroup.MapGet("/", GetLocationAll);
        locationGroup.MapGet("/{id:guid}", GetLocationById);
        locationGroup.MapPost("/", CreateLocation);
        locationGroup.MapPut("/{id:guid}", UpdateLocation);
        locationGroup.MapDelete("/{id:guid}", DeleteLocation);
    }

    private static async Task<IResult> GetLocationAll(
        ILocationService locationService,
        CancellationToken cancellationToken)
    {
        var result = await locationService.GetAllAsync(cancellationToken);
        if (result.IsFailure)
            return TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result));

        var locationDtos = result.Value.ToDto();
        return TypedResults.Ok(locationDtos);
    }

    private static async Task<IResult> GetLocationById(
        Guid id,
        ILocationService locationService,
        CancellationToken cancellationToken)
    {
        var result = await locationService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result));

        var locationDto = result.Value.ToDto();
        return TypedResults.Ok(locationDto);
    }

    private static async Task<IResult> CreateLocation(
        CreateUpdateLocationDto createUpdateLocationDto,
        ILocationService locationService,
        CancellationToken cancellationToken)
    {
        var locationResult = createUpdateLocationDto.ToEntity();
        if (locationResult.IsFailure)
            return TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(locationResult));

        var result = await locationService.CreateAsync(locationResult.Value, cancellationToken);
        return result.IsFailure
            ? TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(result))
            : TypedResults.Created($"/locations/{result.Value.Id}", result.Value.ToDto());
    }

    private static async Task<IResult> UpdateLocation(
        Guid id,
        CreateUpdateLocationDto createUpdateLocationDto,
        ILocationService locationService,
        CancellationToken cancellationToken)
    {
        var locationResult = createUpdateLocationDto.ToEntity();
        if (locationResult.IsFailure)
            return TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(locationResult));

        var result = await locationService.UpdateAsync(id, locationResult.Value, cancellationToken);
        return result.IsFailure
            ? TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result))
            : TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> DeleteLocation(
        Guid id,
        ILocationService locationService,
        CancellationToken cancellationToken)
    {
        var result = await locationService.DeleteAsync(id, cancellationToken);
        return result.IsFailure
            ? TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result))
            : TypedResults.NoContent();
    }
}