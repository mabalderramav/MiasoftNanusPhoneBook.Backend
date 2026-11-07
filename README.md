# MiasoftNanus PhoneBook Backend

The backend of **MiasoftNanus PhoneBook**: a corporate phone book (directory) REST API built with
**ASP.NET Core Minimal APIs on .NET 10**, following **Clean Architecture**, **Domain-Driven Design** and a
hand-written **CQRS** dispatcher (no MediatR). Data is stored in **PostgreSQL** through **Entity Framework Core**,
and agenda photos are stored on the local file system.

The API manages the people of an organization (agendas), where they sit (work areas, workstations, locations)
and the users and profiles that administer the directory.

---

## Table of contents

- [Tech stack](#tech-stack)
- [Solution structure](#solution-structure)
- [Projects](#projects)
  - [MiasoftNanus.PhoneBook.Domain](#miasoftnanusphonebookdomain)
  - [MiasoftNanus.PhoneBook.Application](#miasoftnanusphonebookapplication)
  - [MiasoftNanus.PhoneBook.Infrastructure](#miasoftnanusphonebookinfrastructure)
  - [MiasoftNanus.PhoneBook.WebApi](#miasoftnanusphonebookwebapi)
- [Domain model](#domain-model)
- [Request flow](#request-flow)
- [Getting started](#getting-started)
- [Configuration](#configuration)
- [Database migrations](#database-migrations)
- [API reference](#api-reference)
- [Development workflow](#development-workflow)

---

## Tech stack

| Concern            | Technology                                                        |
|--------------------|-------------------------------------------------------------------|
| Runtime / SDK      | .NET 10 (SDK pinned in `global.json` → `10.0.400`)                |
| Web                | ASP.NET Core Minimal APIs, `Microsoft.AspNetCore.OpenApi`         |
| Persistence        | EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL`              |
| Naming convention  | `EFCore.NamingConventions` (snake_case tables and columns)        |
| Password hashing   | `Microsoft.Extensions.Identity.Core` (`PasswordHasher`)           |
| Validation         | `FluentValidation` + domain validation through `Result` / `Error` |
| Logging            | Serilog (console + daily rolling file)                            |
| File storage       | Local file system (`IFileStorage` → `LocalFileStorage`)           |
| Solution format    | `MiasoftNanusPhoneBook.Backend.slnx`                              |

## Solution structure

```
MiasoftNanusPhoneBook.Backend/
├── .agents/features/                             # Feature specs per ticket (MNPB-xxx.md) + Template.md
├── global.json                                   # Pinned .NET SDK
├── global_command.txt                            # Useful SDK / EF Core commands
├── MiasoftNanusPhoneBook.Backend.slnx            # Solution file
├── MiasoftNanusPhoneBook.Backend.sln.DotSettings # Rider/ReSharper settings
└── src/
    ├── MiasoftNanus.PhoneBook.Domain/            # Entities, value objects, errors, contracts
    ├── MiasoftNanus.PhoneBook.Application/       # Use cases (commands/queries), services, abstractions
    ├── MiasoftNanus.PhoneBook.Infrastructure/    # EF Core, repositories, storage, security
    └── MiasoftNanus.PhoneBook.WebApi/            # Minimal API host, endpoints, DTOs, mappers
```

Dependencies point inward:

```
WebApi ──► Infrastructure ──► Application ──► Domain
   └──────────────────────────► Application
   └─────────────────────────────────────────► Domain
```

The **Domain** has no package or project references.

## Projects

### MiasoftNanus.PhoneBook.Domain

The core of the system. Pure C#, no external dependencies.

- **`Abstractions/`** – building blocks shared by every aggregate:
  - `Entity` (Id + domain events), `IDomainEvent`, `INotification`, `INotificationHandler`, `IPublisher`
  - `Result` / `Result<T>` and `Error` – operations return results instead of throwing exceptions
  - `IGenericRepository`, `IUnitOfWork`, `Services/ICrudService`
- **One folder per aggregate** (`Agendas`, `Locations`, `Profiles`, `Users`, `WorkAreas`, `Workstations`), each with:
  - `Entities/` – the aggregate with private setters and factory methods (`Create`, `Update`, `Patch`)
  - `Errors/` – the typed errors of the aggregate (e.g. `AgendaErrors.PhotoNotFound`)
  - `Repositories/` – repository interfaces
  - `Services/` – service interfaces
  - `Events/` – domain events (e.g. `AgendaCreatedDomainEvent`, `UserCreatedDomainEvent`)
- **Value objects**: `Email` (shared), `Password`, `Username`, `Address` (users).
- **`Shared/Enums/States`** – `Inactive` / `Active`.
- `PhoneBookModel.drawio` – diagram of the domain model.

Ids are generated with `Guid.CreateVersion7()` (time-ordered UUIDs).

### MiasoftNanus.PhoneBook.Application

Use cases and orchestration. References only the Domain.

- **`Abstractions/CQRS/`** – a lightweight CQRS implementation:
  `ICommand<T>` / `ICommandHandler<,>`, `IQuery<T>` / `IQueryHandler<,>`, and `Dispatcher`, which resolves
  the handler from DI (with a cached invoker per message type).
- **`Abstractions/Notifications/Publisher`** – publishes domain notifications to their handlers.
- **`Abstractions/Services/CrudCqrsService`** – base service that maps CRUD operations to commands and queries.
- **Ports implemented by Infrastructure**: `IEmailService`, `IFileStorage` (+ `StoredFile`), `IDateTimeProvider`.
- **One folder per aggregate** with:
  - `Services/` – the application service used by the endpoints (e.g. `AgendaService`)
  - `UseCases/Commands/<Name>/` – command + handler (Create, Update, Patch, Delete, …)
  - `UseCases/Queries/<Name>/` – query + handler (GetAll, GetById, …)
  - `Mappers/` – mapping from commands to domain creation/patch data
- `Agendas/Storage/AgendaPhotoStorage` – shared rules for the `agendas` storage container.
- `DependencyInjection.AddApplication()` registers the dispatcher, publisher, services and **scans the assembly**
  to register every command, query and notification handler automatically.

### MiasoftNanus.PhoneBook.Infrastructure

Technical implementations of the Domain and Application contracts.

- **`AppDbContext`** – EF Core context; also implements `IUnitOfWork`.
- **Per aggregate**: `<Aggregate>Configuration` (EF Core entity mapping) and `<Aggregate>Repository`.
- **`Shared/Repositories/CrudRepository`** – generic base repository.
- **`Abstractions/`**:
  - `Security/PasswordHasher` – hashes passwords with ASP.NET Core Identity's hasher
  - `Storage/LocalFileStorage` + `FileStorageOptions` – saves files with generated names, and refuses paths
    outside the storage root
  - `Time/DateTimeProvider`
  - `Email/EmailService` – placeholder (no-op) implementation
- **`Migrations/`** – EF Core migrations for PostgreSQL.
- `DependencyInjection.AddInfrastructure(configuration)` configures Npgsql with the `DefaultConnection`
  connection string and the snake_case naming convention, and registers repositories and services.

### MiasoftNanus.PhoneBook.WebApi

The ASP.NET Core host and the HTTP surface.

- **`Program.cs`** – bootstraps Serilog, OpenAPI, configuration and the layers; in **Development** it maps the
  OpenAPI document and **applies pending migrations at startup**.
- **`Extensions/`**:
  - `AppLoggingBuilderExtensions` – Serilog to console and `logs/log.txt` (daily rolling)
  - `AppConfigBuilderExtensions` – binds the `API` section to `ApiConfig`, JSON options
  - `AppLayersServicesBuilderExtensions` – calls `AddApplication()` and `AddInfrastructure()`
  - `DataBaseMigrationBuilderExtensions` – `MigrateDatabaseAsync()`
  - `EndpointsBuilderExtensions` – `MapEndpoints()`, maps every endpoint group
- **One folder per resource** (`Agendas`, `Locations`, `Profiles`, `Users`, `WorkAreas`, `Workstations`) with
  `Endpoints/`, `Dtos/` and `Mappers/`.
- **`Healths/`** – health, version, info, status, metrics and config endpoints.
- `Config/ApiConfig` – typed settings (`Timeout`, `BaseUrl`, `Token`).

## Domain model

```
Profile 1 ────< User 1 ────< Agenda >──── 1 Location
                               │
                               ∨
                          1 Workstation >──── 1 WorkArea
```

| Entity          | Main fields                                                                                                                        |
|-----------------|------------------------------------------------------------------------------------------------------------------------------------|
| **Profile**     | `ProfileName`, `Description`                                                                                                       |
| **User**        | `FirstName`, `LastName`, `UserName` (generated: `F.LASTNAME`), `Password` (hashed, min. 8 chars), `Birthdate`, `Email`, `Address` (Country, State, Province, District, Street), `States`, `DateOfLastChange`, `ProfileId` |
| **WorkArea**    | `Code`, `Name`, `Description`                                                                                                      |
| **Workstation** | `Code`, `Name`, `Description`, `WorkAreaId`                                                                                        |
| **Location**    | `Code`, `Description`                                                                                                              |
| **Agenda**      | `Code`, `FullName`, `Email`, `Enabled`, `ImagePath`, `DirectLine`, `Internal`, `Corporate`, `DirectAccess`, `WorkstationId`, `LocationId`, `UserIdCreated` |

## Request flow

```
HTTP request
  → Endpoint (WebApi)           maps DTO → command/query
  → Service (Application)       e.g. AgendaService
  → Dispatcher                  resolves ICommandHandler / IQueryHandler
  → Handler                     loads aggregate through a repository
  → Entity (Domain)             validates and changes state, returns Result
  → IUnitOfWork.SaveChanges     (AppDbContext)
  ← Result → TypedResults (200/201/204/400/404) with DTO or Error
```

## Getting started

### Prerequisites

- [.NET SDK 10.0.400](https://dotnet.microsoft.com/download) (see `global.json`)
- PostgreSQL
- EF Core CLI (for migrations): `dotnet tool install --global dotnet-ef`

### Build

```bash
dotnet restore MiasoftNanusPhoneBook.Backend.slnx
dotnet build MiasoftNanusPhoneBook.Backend.slnx
```

### Run

The connection string is empty in `appsettings.Development.json`; provide it with **user-secrets** (recommended)
or on the command line.

```bash
# Store the connection string in user-secrets (once)
dotnet user-secrets --project ./src/MiasoftNanus.PhoneBook.WebApi/MiasoftNanus.PhoneBook.WebApi.csproj \
  set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=phonebook;Username=postgres;Password=<password>"

# Run with a launch profile: Development | Staging | Production
dotnet run --project ./src/MiasoftNanus.PhoneBook.WebApi/MiasoftNanus.PhoneBook.WebApi.csproj --launch-profile Development

# Or pass the connection string directly
dotnet run --project ./src/MiasoftNanus.PhoneBook.WebApi/MiasoftNanus.PhoneBook.WebApi.csproj --launch-profile Development \
  -- --ConnectionStrings:DefaultConnection='<connection string>'
```

| Profile     | HTTP                    | HTTPS                    |
|-------------|-------------------------|--------------------------|
| Development | `http://localhost:5300` | `https://localhost:5301` |
| Staging     | `http://localhost:5302` | `https://localhost:5303` |
| Production  | `http://localhost:5304` | `https://localhost:5305` |

In Development the OpenAPI document is available at `/openapi/v1.json`, and migrations are applied automatically.

## Configuration

| Key                                    | Where                          | Description                                          |
|----------------------------------------|--------------------------------|------------------------------------------------------|
| `ConnectionStrings:DefaultConnection`  | user-secrets / env / CLI       | PostgreSQL connection string                         |
| `FileStorage:RootPath`                 | `appsettings.json`             | Root folder for uploaded files (default `Storage`)   |
| `API:Timeout`, `API:BaseUrl`           | `appsettings.{Environment}.json` | Bound to `ApiConfig`                               |
| `API:Token`                            | user-secrets                   | Bound to `ApiConfig.Token`                           |

Logs are written to the console and to `logs/log.txt` under the application's base directory.

## Database migrations

Run from the repository root (`-p` = migrations project, `-s` = startup project):

```bash
# Add a migration
dotnet ef migrations add <Name> -p src/MiasoftNanus.PhoneBook.Infrastructure/ -s src/MiasoftNanus.PhoneBook.WebApi/

# Apply migrations (also done automatically in Development)
dotnet ef database update -p src/MiasoftNanus.PhoneBook.Infrastructure/ -s src/MiasoftNanus.PhoneBook.WebApi/

# Remove the last migration
dotnet ef migrations remove -p src/MiasoftNanus.PhoneBook.Infrastructure/ -s src/MiasoftNanus.PhoneBook.WebApi/
```

## API reference

All ids are GUIDs. Errors are returned as an `Error` object with `400 Bad Request` or `404 Not Found`.

### Health

| Verb | Route      | Description                                  |
|------|------------|----------------------------------------------|
| GET  | `/`        | Welcome message                              |
| GET  | `/health`  | `{ "status": "Healthy" }`                    |
| GET  | `/version` | Assembly version                             |
| GET  | `/info`    | API name, version and description            |
| GET  | `/status`  | Running status and UTC timestamp             |
| GET  | `/metrics` | Uptime, memory, threads (Development only)   |
| GET  | `/config`  | Environment and `ApiConfig` (Development only) |

### Profiles, Locations, Work areas, Workstations

Same CRUD shape for each resource: `/profiles`, `/locations`, `/work-areas`, `/workstations`.

| Verb   | Route            | Description   |
|--------|------------------|---------------|
| GET    | `/{resource}`        | List all      |
| GET    | `/{resource}/{id}`   | Get by id     |
| POST   | `/{resource}`        | Create        |
| PUT    | `/{resource}/{id}`   | Update        |
| DELETE | `/{resource}/{id}`   | Delete        |

### Users

| Verb   | Route                   | Description                                                  |
|--------|-------------------------|--------------------------------------------------------------|
| GET    | `/users`                | List all                                                     |
| GET    | `/users/{id}`           | Get by id                                                    |
| POST   | `/users`                | Create (username generated, password hashed)                 |
| PUT    | `/users/{id}`           | Full update                                                  |
| PATCH  | `/users/{id}`           | Partial update (only the fields sent)                        |
| PUT    | `/users/{id}/password`  | Change password                                              |
| DELETE | `/users/{id}`           | Delete                                                       |

### Agendas

| Verb   | Route                  | Description                                                                  |
|--------|------------------------|------------------------------------------------------------------------------|
| GET    | `/agendas`             | List all (with embedded workstation, location and creator)                   |
| GET    | `/agendas/{id}`        | Get by id                                                                    |
| POST   | `/agendas`             | Create                                                                       |
| PUT    | `/agendas/{id}`        | Full update                                                                  |
| PATCH  | `/agendas/{id}`        | Partial update (`application/merge-patch+json` or `application/json`)        |
| DELETE | `/agendas/{id}`        | Delete (also deletes the stored photo)                                       |
| PUT    | `/agendas/{id}/photo`  | Upload / replace photo (`multipart/form-data`; `.jpg`, `.jpeg`, `.png`, `.webp`; max 2 MB) |
| GET    | `/agendas/{id}/photo`  | Download the photo                                                           |
| DELETE | `/agendas/{id}/photo`  | Delete the photo                                                             |

Example:

```bash
curl -k https://localhost:5301/agendas
curl -k -X PUT -F "photo=@photo.jpg" https://localhost:5301/agendas/<id>/photo
curl -k -X DELETE https://localhost:5301/agendas/<id>/photo
```

## Development workflow

- Work is tracked by tickets **`MNPB-xxx`**. Each feature has a spec in `.agents/features/MNPB-xxx.md`
  (Summary, Motivation, Implementation, Scope, Notes) based on `.agents/features/Template.md`.
- Branch and commit/PR titles use the ticket: `MNPB-143 WebApi - Delete-photo use case …`.
- Adding a new use case:
  1. Domain – add behavior to the entity and new `Errors` if needed.
  2. Application – add `XxxCommand`/`XxxQuery` + handler under `UseCases/` (registered automatically) and expose it
     from the service.
  3. Infrastructure – update the repository/configuration and add a migration if the schema changes.
  4. WebApi – add the DTO, mapper and endpoint.
- Helper command snippets live in `global_command.txt` and in each project's `*_command.txt` file.
