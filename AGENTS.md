# AGENTS.md — MiasoftNanusPhoneBook.Backend

Guidance for AI coding agents working in this repository. Read this first. `README.md` has the full human
documentation (domain model, API tables, configuration).

## What this is

REST API for a corporate phone book (directory). It manages agendas (directory entries), work areas,
workstations, locations, users and profiles.

- **.NET 10** (SDK pinned in `global.json` → `10.0.400`), C# with `Nullable` enabled, ASP.NET Core **Minimal APIs**
- **Clean Architecture + DDD + hand-written CQRS**. There is **no MediatR**. Do not add it.
- **PostgreSQL** through **EF Core 10** (Npgsql) with the **snake_case** naming convention
- Serilog logging; OpenAPI (`Microsoft.AspNetCore.OpenApi`) shown with Scalar, Swagger UI and ReDoc
- Solution file: `MiasoftNanusPhoneBook.Backend.slnx`

## Commands

Run these from the repository root.

```bash
dotnet build MiasoftNanusPhoneBook.Backend.slnx

# Run (profiles: Development :5300, Staging :5302, Production :5304; HTTP only)
dotnet run --project src/MiasoftNanus.PhoneBook.WebApi --launch-profile Development

# EF Core migrations (-p = migrations project, -s = startup project)
dotnet ef migrations add <Name> -p src/MiasoftNanus.PhoneBook.Infrastructure/ -s src/MiasoftNanus.PhoneBook.WebApi/
dotnet ef migrations remove     -p src/MiasoftNanus.PhoneBook.Infrastructure/ -s src/MiasoftNanus.PhoneBook.WebApi/
dotnet ef database update       -p src/MiasoftNanus.PhoneBook.Infrastructure/ -s src/MiasoftNanus.PhoneBook.WebApi/
```

- There is **no test project** yet. Check your work by running `dotnet build` with **zero new warnings**.
- The connection string (`ConnectionStrings:DefaultConnection`) is **not** committed. It comes from
  user-secrets, environment variables or the CLI. Never write secrets into `appsettings*.json`.
- In Development, pending migrations are **applied automatically at startup** (`MigrateDatabaseAsync`).
- OpenAPI document: `/openapi/v1.json`. UIs: `/scalar`, `/swagger`, `/redoc`.

## Layout and dependency rules

```
src/
├── MiasoftNanus.PhoneBook.Domain/          # Entities, value objects, Result/Error, repository & service interfaces
├── MiasoftNanus.PhoneBook.Application/     # CQRS commands/queries + handlers, services, ports, pipeline behaviors
├── MiasoftNanus.PhoneBook.Infrastructure/  # AppDbContext (also IUnitOfWork), EF configurations, repositories, storage
└── MiasoftNanus.PhoneBook.WebApi/          # Program.cs, endpoints, DTOs, mappers, extensions
```

Dependencies point inward: `WebApi → Infrastructure → Application → Domain`.

- **Domain has no package or project references.** Keep it pure C#.
- Application references only Domain. Infrastructure implements Domain and Application interfaces.
- Each layer is organized **by aggregate/feature folder**: `Agendas`, `Locations`, `Profiles`, `Users`,
  `WorkAreas`, `Workstations`. Put new code in the matching feature folder, not in a shared "Services" folder.

## Core patterns (follow them exactly)

### Domain
- Entities derive from `Entity`. They have a **private constructor**, **private setters**, a static
  `Create(...)` factory and instance `Update(...)` / `Patch(...)` methods. These methods return
  `Result` / `Result<T>`.
- Ids come from `Guid.CreateVersion7()`.
- **Do not throw for business rules.** Return `Result.Failure(...)` with an error from `<Aggregate>Errors`
  (for example `LocationErrors.CodeCannotBeNullOrWhitespace`). Error codes use the format `"<Aggregate>Errors.<Name>"`.
- Validation lives in the entity (`private static Result Validate(...)`). Value objects: `Email`, `Password`,
  `Username`, `Address`.

### Application (CQRS)
- Use cases live in `UseCases/Commands/<Name>/` and `UseCases/Queries/<Name>/`. Each folder holds a
  `record XxxCommand(...) : ICommand<Result<T>>` (or `IQuery<...>`) and a `sealed class XxxCommandHandler`
  with a primary constructor.
- Handlers are **registered automatically** by assembly scanning in `Application/DependencyInjection.cs`. Do not
  register them by hand.
- Command handlers call `unitOfWork.SaveChangesAsync(ct)` and return `Error.CannotSaveChanges` when it reports
  0 rows saved.
- Endpoints call an **application service** (for example `LocationService : CrudCqrsService<Location, Guid>`),
  which builds commands and queries and sends them through `IDispatcher`. Register new services in
  `AddApplication()`.
- Command pipeline behaviors (`ICommandPipelineBehavior<,>`, for example `LoggingBehavior`) are registered in
  `AddApplication()`. **The order of registration is the order of wrapping.**

### Infrastructure
- One `<Aggregate>Configuration` (EF mapping) and one `<Aggregate>Repository` per aggregate. Use
  `Shared/Repositories/CrudRepository` as the base.
- If you change the schema, **add a migration** (see Commands). Never edit an existing migration that has
  already been applied.

### WebApi
- Each resource has `Endpoints/<Resource>Endpoints.cs` with a `Map<Resource>Endpoints` extension, and the
  group is wired in `Extensions/EndpointsBuilderExtensions.cs`.
- Routes: `app.MapGroup("/api/v1/<resource>")` with `.WithGroupName("v1")`, `.WithTags(...)` and
  `.WithDescription(...)`. Id routes use `{id:guid}`. Plural, kebab-case resource names
  (for example `/api/v1/work-areas`).
- Every endpoint declares OpenAPI metadata: `.WithSummary`, `.WithDescription`, `.Produces<TDto>(...)`,
  `.ProducesProblem(400/404)`.
- Handlers are `private static async Task<IResult>` and return `TypedResults`.
- **All error responses are `ProblemDetails`**, built with `Shared/ProblemDetailsHelper`:
  `CreateBadRequestProblemDetails`, `CreateNotFoundProblemDetails`, or
  `ToErrorResult(result, notFoundErrors...)` (404 if the error is in the list, otherwise 400).
  Do not return raw `Error` objects.
- DTOs go in `Dtos/`. Mapping is done with hand-written extension methods in `Mappers/` (`ToDto()`,
  `ToEntity()`, ...). There is no AutoMapper.
- Cross-cutting setup goes in `Extensions/App*BuilderExtensions.cs` and is called from `Program.cs`. Keep
  `Program.cs` thin.
- CORS policy: `PolicyName.AllowSpecificOrigin` (currently `http://localhost:4200`, the frontend).

## Code style

- File-scoped namespaces that match the folder path (`MiasoftNanus.PhoneBook.<Layer>.<Feature>...`).
- **XML doc comments (`/// <summary>`) on public types and members.** The existing code documents everything,
  so keep that up.
- Primary constructors for DI. `sealed` handlers. `record` commands, queries and DTOs.
- Pass a `CancellationToken` all the way down through every async call.
- Respect `MiasoftNanusPhoneBook.Backend.sln.DotSettings` (Rider/ReSharper).

## Adding a feature (checklist)

1. **Domain**: add behavior to the entity and new `<Aggregate>Errors` entries.
2. **Application**: add a command or query + handler under `UseCases/`, and expose it from the service and its
   `I<Aggregate>Service` interface (the interface lives in Domain).
3. **Infrastructure**: update the repository and configuration, and add a migration if the schema changes.
4. **WebApi**: add the DTO, mapper and endpoint (with OpenAPI metadata and ProblemDetails errors).
5. Run `dotnet build` and fix any new warnings. Update `README.md` if routes or configuration change.

## Workflow conventions

- Work is tracked by tickets. The current prefix is **`MN-xxx`** (older work uses `MNPB-xxx`).
- A feature spec or plan goes in `.agents/features/<TICKET>.md`, based on `.agents/features/Template.md`
  (Summary, Motivation, Implementation, Scope, Notes). Read the spec for the ticket you are working on, and
  write one when asked to plan.
- Commit and PR titles: `<TICKET> <Layer> - <short description>`, for example
  `MN-19 WebApi - Add Cors basic configuration`.
- Don't commit `bin/`, `obj/`, `logs/`, `Storage/` (uploaded files) or secrets.
- Command snippets: `global_command.txt` and the `*_command.txt` file in each project.

## Gotchas

- `README.md` may lag behind the code (for example, routes are now prefixed with `/api/v1`, launch profiles are
  HTTP only, and errors are `ProblemDetails`). **Trust the code.**
- Agenda photos are stored on the local file system under `FileStorage:RootPath` (`IFileStorage` →
  `LocalFileStorage`, which refuses paths outside the root). Deleting an agenda also deletes its photo.
- `EmailService` is a no-op placeholder.
- The agenda delete endpoints are documented as requiring an admin `X-Api-Key` header. Check how this is
  enforced before you change those endpoints.
