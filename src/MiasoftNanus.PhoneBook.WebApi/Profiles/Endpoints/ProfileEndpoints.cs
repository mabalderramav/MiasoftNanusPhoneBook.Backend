using MiasoftNanus.PhoneBook.Domain.Profiles.Services;
using MiasoftNanus.PhoneBook.WebApi.Profiles.Dtos;
using MiasoftNanus.PhoneBook.WebApi.Profiles.Mappers;
using MiasoftNanus.PhoneBook.WebApi.Shared;

namespace MiasoftNanus.PhoneBook.WebApi.Profiles.Endpoints;

/// <summary>
/// Provides extension methods for mapping profile-related endpoints.
/// </summary>
public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var profileGroup = app.MapGroup("/api/v1/profiles")
            .WithGroupName("v1")
            .WithTags("Profiles")
            .WithDescription("Endpoints for managing profiles.");
        profileGroup.MapGet("/", GetProfileAll)
            .WithSummary("Get all profiles")
            .WithDescription("Gets all profiles.")
            .Produces<IEnumerable<ProfileDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        profileGroup.MapGet($"/{{id:guid}}", GetProfileById)
            .WithSummary("Get profile by ID")
            .WithDescription("Gets a profile by its ID.")
            .Produces<ProfileDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        profileGroup.MapPost("/", CreateProfile)
            .WithSummary("Create profile")
            .WithDescription("Creates a new profile.")
            .Produces<ProfileDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        profileGroup.MapPut($"/{{id:guid}}", UpdateProfile)
            .WithSummary("Update profile")
            .WithDescription("Updates an existing profile.")
            .Produces<ProfileDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        profileGroup.MapDelete($"/{{id:guid}}", DeleteProfile)
            .WithSummary("Delete profile")
            .WithDescription("Deletes a profile by its ID.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetProfileAll(
        IProfileService profileService,
        CancellationToken cancellationToken)
    {
        var result = await profileService.GetAllAsync(cancellationToken);
        if (result.IsFailure)
            return TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result));

        var profileDtos = result.Value.ToDto();
        return TypedResults.Ok(profileDtos);
    }

    private static async Task<IResult> GetProfileById(
        Guid id,
        IProfileService profileService,
        CancellationToken cancellationToken)
    {
        var result = await profileService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result));
        
        var profileDto = result.Value.ToDto();
        return TypedResults.Ok(profileDto);
    }

    private static async Task<IResult> CreateProfile(
        CreateUpdateProfileDto createUpdateProfileDto,
        IProfileService profileService,
        CancellationToken cancellationToken)
    {
        var resultProfile = createUpdateProfileDto.ToEntity();
        if(resultProfile.IsFailure)
            return TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(resultProfile));

        var result = await profileService.CreateAsync(resultProfile.Value, cancellationToken);
        return result.IsFailure
            ? TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(result))
            : TypedResults.Created($"/api/v1/profiles/{result.Value.Id}", result.Value.ToDto());
    }

    private static async Task<IResult> UpdateProfile(
        Guid id,
        CreateUpdateProfileDto createUpdateProfileDto,
        IProfileService profileService,
        CancellationToken cancellationToken)
    {
        var resultProfile = createUpdateProfileDto.ToEntity();
        if(resultProfile.IsFailure)
            return TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(resultProfile));
        
        var result = await profileService.UpdateAsync(id, resultProfile.Value, cancellationToken);
        return result.IsFailure
            ? TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result))
            : TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> DeleteProfile(
        Guid id,
        IProfileService profileService,
        CancellationToken cancellationToken)
    {
        var result = await profileService.DeleteAsync(id, cancellationToken);
        return result.IsFailure
            ? TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result))
            : TypedResults.NoContent();
    }
}
