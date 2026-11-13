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
        var locationGroup = app.MapGroup("/api/v1/locations")
            .WithGroupName("v1")
            .WithTags("Locations")
            .WithDescription("Endpoints for managing locations.");
        locationGroup.MapGet("/", GetLocationAll)
            .WithSummary("Get all locations")
            .WithDescription("Gets all locations.")
            .Produces<IEnumerable<LocationDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        locationGroup.MapGet($"/{{id:guid}}", GetLocationById)
            .WithSummary("Get location by ID")
            .WithDescription("Gets a location by its ID.")
            .Produces<LocationDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        locationGroup.MapPost("/", CreateLocation)
            .WithSummary("Create location")
            .WithDescription("Creates a new location.")
            .Produces<LocationDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        locationGroup.MapPut($"/{{id:guid}}", UpdateLocation)
            .WithSummary("Update location")
            .WithDescription("Updates an existing location.")
            .Produces<LocationDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        locationGroup.MapDelete($"/{{id:guid}}", DeleteLocation)
            .WithSummary("Delete location")
            .WithDescription("Deletes a location by its ID.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
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
            : TypedResults.Created($"/api/v1/locations/{result.Value.Id}", result.Value.ToDto());
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