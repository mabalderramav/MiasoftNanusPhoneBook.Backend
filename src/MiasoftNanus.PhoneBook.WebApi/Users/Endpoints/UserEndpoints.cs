using MiasoftNanus.PhoneBook.Application.Abstractions.Time;
using MiasoftNanus.PhoneBook.Domain.Users.Errors;
using MiasoftNanus.PhoneBook.Domain.Users.ObjectValues;
using MiasoftNanus.PhoneBook.Domain.Users.Services;
using MiasoftNanus.PhoneBook.WebApi.Shared;
using MiasoftNanus.PhoneBook.WebApi.Users.Dtos;
using MiasoftNanus.PhoneBook.WebApi.Users.Mappers;

namespace MiasoftNanus.PhoneBook.WebApi.Users.Endpoints;

/// <summary>
/// Provides extension methods for mapping user-related endpoints.
/// </summary>
public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var userGroup = app.MapGroup("/users");
        userGroup.MapGet("/", GetUserAll)
            .ProducesProblem(StatusCodes.Status404NotFound);
        userGroup.MapGet($"/{{id:guid}}", GetUserById)
            .ProducesProblem(StatusCodes.Status404NotFound);
        userGroup.MapPost("/", CreateUser)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        userGroup.MapPut($"/{{id:guid}}", UpdateUser)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        userGroup.MapPatch($"/{{id:guid}}", PatchUser)
            .Accepts<PatchUserDto>("application/merge-patch+json", "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        userGroup.MapPut($"/{{id:guid}}/password", ChangeUserPassword)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        userGroup.MapDelete($"/{{id:guid}}", DeleteUser)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetUserAll(
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var result = await userService.GetAllAsync(cancellationToken);
        if (result.IsFailure)
            return TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result));

        return TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> GetUserById(
        Guid id,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var result = await userService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result));

        return TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> CreateUser(
        CreateUserDto createUserDto,
        IUserService userService,
        IDateTimeProvider dateTimeProvider,
        CancellationToken cancellationToken)
    {
        var userResult = createUserDto.ToEntity(dateTimeProvider.CurrentTime);
        if (userResult.IsFailure)
            return TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(userResult));

        var result = await userService.CreateAsync(userResult.Value, cancellationToken);
        return result.IsFailure
            ? TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(result))
            : TypedResults.Created($"/users/{result.Value.Id}", result.Value.ToDto());
    }

    private static async Task<IResult> UpdateUser(
        Guid id,
        UpdateUserDto updateUserDto,
        IUserService userService,
        IDateTimeProvider dateTimeProvider,
        CancellationToken cancellationToken)
    {
        var userDataResult = updateUserDto.ToUserData(dateTimeProvider.CurrentTime);
        if (userDataResult.IsFailure)
            return TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(userDataResult));

        var result = await userService.UpdateDetailsAsync(id, userDataResult.Value, cancellationToken);
        if (result.IsFailure)
            return ProblemDetailsHelper.ToErrorResult(result, UserErrors.UserNotFound);

        return TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> PatchUser(
        Guid id,
        PatchUserDto patchUserDto,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var result = await userService.PatchAsync(id, patchUserDto.ToUserPatchData(), cancellationToken);
        if (result.IsFailure)
            return ProblemDetailsHelper.ToErrorResult(result, UserErrors.UserNotFound);

        return TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> ChangeUserPassword(
        Guid id,
        ChangeUserPasswordDto changeUserPasswordDto,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        // Early 400 on the raw value, before any lookup or hashing.
        var passwordResult = Password.Create(changeUserPasswordDto.Password);
        if (passwordResult.IsFailure)
            return TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(passwordResult));

        var result = await userService.ChangePasswordAsync(id, changeUserPasswordDto.Password, cancellationToken);
        if (result.IsFailure)
            return ProblemDetailsHelper.ToErrorResult(result, UserErrors.UserNotFound);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteUser(
        Guid id,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var result = await userService.DeleteAsync(id, cancellationToken);
        return result.IsFailure
            ? TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result))
            : TypedResults.NoContent();
    }
}
