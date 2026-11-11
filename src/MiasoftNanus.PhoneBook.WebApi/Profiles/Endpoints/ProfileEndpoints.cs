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
        var profileGroup = app.MapGroup("/profiles");
        profileGroup.MapGet("/", GetProfileAll)
            .ProducesProblem(StatusCodes.Status404NotFound);
        profileGroup.MapGet($"/{{id:guid}}", GetProfileById)
            .ProducesProblem(StatusCodes.Status404NotFound);
        profileGroup.MapPost("/", CreateProfile)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        profileGroup.MapPut($"/{{id:guid}}", UpdateProfile)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        profileGroup.MapDelete($"/{{id:guid}}", DeleteProfile)
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
            : TypedResults.Created($"/profiles/{result.Value.Id}", result.Value);
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
            : TypedResults.Ok(result.Value);
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
