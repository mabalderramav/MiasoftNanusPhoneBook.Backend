using MiasoftNanus.PhoneBook.Domain.Agendas.Errors;
using MiasoftNanus.PhoneBook.Domain.Agendas.Services;
using MiasoftNanus.PhoneBook.WebApi.Agendas.Dtos;
using MiasoftNanus.PhoneBook.WebApi.Agendas.Mappers;
using MiasoftNanus.PhoneBook.WebApi.Config;
using MiasoftNanus.PhoneBook.WebApi.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace MiasoftNanus.PhoneBook.WebApi.Agendas.Endpoints;

/// <summary>
/// Provides extension methods for mapping agenda-related endpoints.
/// </summary>
public static class AgendaEndpoints
{
    // Multipart overhead on top of the 2 MB photo rule of UploadPhotoAgendaCommandHandler.
    private const long MaxPhotoRequestSize = 3 * 1024 * 1024;

    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    public static void MapAgendaEndpoints(this IEndpointRouteBuilder app)
    {
        var agendaGroup = app.MapGroup("/agendas");
        agendaGroup.MapGet("/", GetAgendaAll)
            .ProducesProblem(StatusCodes.Status404NotFound);
        agendaGroup.MapGet($"/{{id:guid}}", GetAgendaById)
            .ProducesProblem(StatusCodes.Status404NotFound);
        agendaGroup.MapPost("/", CreateAgenda)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        agendaGroup.MapPut($"/{{id:guid}}", UpdateAgenda)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        agendaGroup.MapPatch($"/{{id:guid}}", PatchAgenda)
            .Accepts<PatchAgendaDto>("application/merge-patch+json", "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        agendaGroup.MapDelete($"/{{id:guid}}", DeleteAgenda)
            .ProducesProblem(StatusCodes.Status404NotFound);
        agendaGroup.MapPut($"/{{id:guid}}/photo", UploadPhotoAgenda)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxPhotoRequestSize))
            .WithFormOptions(multipartBodyLengthLimit: MaxPhotoRequestSize)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        agendaGroup.MapGet($"/{{id:guid}}/photo", GetPhotoAgenda)
            .ProducesProblem(StatusCodes.Status404NotFound);
        agendaGroup.MapDelete($"/{{id:guid}}/photo", DeletePhotoAgenda)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetAgendaAll(
        IAgendaService agendaService,
        CancellationToken cancellationToken)
    {
        var result = await agendaService.GetAllAsync(cancellationToken);
        if (result.IsFailure)
            return TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result));

        return TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> GetAgendaById(
        Guid id,
        IAgendaService agendaService,
        CancellationToken cancellationToken)
    {
        var result = await agendaService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result));

        return TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> CreateAgenda(
        CreateUpdateAgendaDto createUpdateAgendaDto,
        IAgendaService agendaService,
        CancellationToken cancellationToken)
    {
        var agendaResult = createUpdateAgendaDto.ToEntity();
        if (agendaResult.IsFailure)
            return TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(agendaResult));

        var result = await agendaService.CreateAsync(agendaResult.Value, cancellationToken);
        return result.IsFailure
            ? TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(result))
            : TypedResults.Created($"/agendas/{result.Value.Id}", result.Value.ToDto());
    }

    private static async Task<IResult> UpdateAgenda(
        Guid id,
        CreateUpdateAgendaDto createUpdateAgendaDto,
        IAgendaService agendaService,
        CancellationToken cancellationToken)
    {
        var agendaResult = createUpdateAgendaDto.ToEntity();
        if (agendaResult.IsFailure)
            return TypedResults.BadRequest(ProblemDetailsHelper.CreateBadRequestProblemDetails(agendaResult));

        var result = await agendaService.UpdateAsync(id, agendaResult.Value, cancellationToken);
        if (result.IsFailure)
            return ProblemDetailsHelper.ToErrorResult(result, AgendaErrors.AgendaNotFound, AgendaErrors.PhotoNotFound);

        return TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> PatchAgenda(
        Guid id,
        PatchAgendaDto patchAgendaDto,
        IAgendaService agendaService,
        CancellationToken cancellationToken)
    {
        var result = await agendaService.PatchAsync(id, patchAgendaDto.ToAgendaPatchData(), cancellationToken);
        if (result.IsFailure)
            return ProblemDetailsHelper.ToErrorResult(result, AgendaErrors.AgendaNotFound, AgendaErrors.PhotoNotFound);

        return TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> UploadPhotoAgenda(
        Guid id,
        IFormFile photo,
        IAgendaService agendaService,
        CancellationToken cancellationToken)
    {
        await using var content = photo.OpenReadStream();
        var result = await agendaService.UploadPhotoAsync(
            id, content, photo.FileName, photo.Length, cancellationToken);
        if (result.IsFailure)
            return ProblemDetailsHelper.ToErrorResult(result, AgendaErrors.AgendaNotFound, AgendaErrors.PhotoNotFound);

        return TypedResults.Ok(result.Value.ToDto());
    }

    private static async Task<IResult> GetPhotoAgenda(
        Guid id,
        IAgendaService agendaService,
        CancellationToken cancellationToken)
    {
        var result = await agendaService.GetPhotoAsync(id, cancellationToken);
        if (result.IsFailure)
            return TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result));

        if (!ContentTypeProvider.TryGetContentType(result.Value.FileName, out var contentType))
            contentType = "application/octet-stream";

        // TypedResults.Stream disposes of the stream after the response is written.
        return TypedResults.Stream(result.Value.Content, contentType);
    }

    private static async Task<IResult> DeletePhotoAgenda(
        Guid id,
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        IOptions<ApiConfig> apiConfig,
        IAgendaService agendaService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey != apiConfig.Value.TokenAdmin)
        {
            return TypedResults.Unauthorized();
        }

        var result = await agendaService.DeletePhotoAsync(id, cancellationToken);
        if (result.IsFailure)
            return ProblemDetailsHelper.ToErrorResult(result, AgendaErrors.AgendaNotFound, AgendaErrors.PhotoNotFound);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteAgenda(
        Guid id,
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        IOptions<ApiConfig> apiConfig,
        IAgendaService agendaService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey != apiConfig.Value.TokenAdmin)
        {
            return TypedResults.Unauthorized();
        }

        var result = await agendaService.DeleteAsync(id, cancellationToken);
        return result.IsFailure
            ? TypedResults.NotFound(ProblemDetailsHelper.CreateNotFoundProblemDetails(result))
            : TypedResults.NoContent();
    }
}