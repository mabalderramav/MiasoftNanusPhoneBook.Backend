# MNPB-132 Application - Implement LoggingBehavior without MediatR and FluentValidation

## Summary

Add a **logging behavior** that wraps every command handler, the way MediatR's `IPipelineBehavior` does, built on
the project's own CQRS `Dispatcher` (no MediatR, no FluentValidation).

Reference (MediatR version, from another project):

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    // LogInformation("Ejecutando command: ...") → await next() → LogInformation("Command ejecutado: ...")
    // catch → LogError(ex, "Error ejecutando command: ...") and rethrow
}
```

After this ticket:

- New namespace **`MiasoftNanus.PhoneBook.Application.Abstractions.Behaviors`** with:
  - `CommandHandlerDelegate<TResult>`: the "next" step in the pipeline (same role as MediatR's
    `RequestHandlerDelegate<TResponse>`).
  - `ICommandPipelineBehavior<TCommand, TResult>`: our own version of `IPipelineBehavior`, for commands only.
  - `LoggingBehavior<TCommand, TResult>`: logs start, end (with elapsed time), `Result` failures and exceptions.
- **`Dispatcher`** runs the registered behaviors around the command handler (outermost = registered first).
- **`AddApplication`** registers `LoggingBehavior<,>` as an open generic.
- Application gets one new package: `Microsoft.Extensions.Logging.Abstractions` (Microsoft, no third party).
- Queries, handlers, endpoints and the HTTP contract do not change.

Sample console output (Serilog):

```
[INF] Executing command CreateAgendaCommand
[INF] Command CreateAgendaCommand executed in 14.2 ms
[WRN] Command DeleteAgendaCommand failed with error AgendaErrors.AgendaNotFound in 3.1 ms
[ERR] Error executing command CreateUserCommand after 5012.7 ms
      Microsoft.Data.SqlClient.SqlException ...
```

## Motivation

### Why

Today the only log lines are Serilog's startup message and `GlobalExceptionHandler` (unhandled exceptions, with
method and path only). Nothing tells which use case ran, how long it took, or that it returned a business failure
(`Result.Failure`). Handlers have no `ILogger`, and adding one to each handler would repeat the same code ~20 times.
A behavior adds it once, for every command, current and future.

### Why our own pipeline instead of MediatR

| Option                                                                                      | Verdict |
|---------------------------------------------------------------------------------------------|---------|
| **`ICommandPipelineBehavior<,>` resolved by `Dispatcher` (open-generic registration)**      | ✅ chosen |
| Add MediatR and port the example as is                                                      | ❌ the project already has `IDispatcher` / `ICommandHandler`; MediatR is commercial since v13 and would replace the CQRS layer |
| Decorator class per handler (`LoggingCommandHandlerDecorator`) wrapped at registration time | ❌ without Scrutor the DI loop must build factories by hand; adding a 2nd cross-cutting concern means editing the registration again |
| `ILogger` inside each handler                                                               | ❌ repeated code, easy to forget in new handlers |

The chosen shape is the MediatR one (`HandleAsync(command, next, ct)`), so the example code translates almost line by
line, and a later `ValidationBehavior` / `UnitOfWorkBehavior` is just a new class + one registration line.

### Why commands only

The example is restricted to commands (`where TRequest : IBaseCommand`). Commands change state and are the ones worth
tracing; logging every `GET` adds noise. The design keeps a query pipeline open (see Notes).

### Changes compared to the example

1. **Structured logging** — message templates with `{CommandName}` instead of interpolated strings
   (`$"... {requestName}"`). Serilog keeps `CommandName` as a property, and it avoids the CA2254 warning.
2. **`Result` failures are logged** — handlers here do not throw for business errors, they return
   `Result.Failure(error)`. The behavior logs those as **Warning** with `Error.Code`; without this, a failed command
   would log "executed" like a successful one.
3. **Elapsed time** — `Stopwatch.GetTimestamp()` / `Stopwatch.GetElapsedTime()` (no allocation).
4. **Cancellation is not an error** — `OperationCanceledException` (client closed the request) is logged as
   Information, not Error.
5. **The command payload is never logged** — only the type name. Commands carry passwords
   (`CreateUserCommand`, `ChangeUserPasswordCommand`) and personal data.
6. English messages, like the rest of the code base.

## Implementation

Order: package → Behaviors → Dispatcher → DI. The build should be green after each step.

### Step 1 — Package

Add `Microsoft.Extensions.Logging.Abstractions` to the Application project, same major/minor as the existing
`Microsoft.Extensions.DependencyInjection.Abstractions` (10.0.x):

```bash
dotnet add ./src/MiasoftNanus.PhoneBook.Application/MiasoftNanus.PhoneBook.Application.csproj package Microsoft.Extensions.Logging.Abstractions --version 10.0.13
```

`MiasoftNanus.PhoneBook.Application.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="FluentValidation" Version="12.2.0" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.13" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.13" />
</ItemGroup>
```

- Abstractions only: Serilog stays in WebApi (`AppLoggingBuilderExtensions`) and is plugged in through
  `builder.Logging.AddSerilog(...)`, so `ILogger<T>` in Application writes to the Serilog console and file sinks.
- Add the command to `src/MiasoftNanus.PhoneBook.Application/command_application.txt` (Windows + Linux lines, like
  the existing entries).

### Step 2 — Behaviors (new folder `Abstractions/Behaviors/`)

1. **`Abstractions/Behaviors/CommandHandlerDelegate.cs`**

```csharp
namespace MiasoftNanus.PhoneBook.Application.Abstractions.Behaviors;

/// <summary>
/// Represents the next step of the command pipeline: the next behavior, or the command handler itself.
/// </summary>
/// <typeparam name="TResult">The type of the result produced by the command.</typeparam>
/// <returns>A task containing the result of the rest of the pipeline.</returns>
public delegate Task<TResult> CommandHandlerDelegate<TResult>();
```

2. **`Abstractions/Behaviors/ICommandPipelineBehavior.cs`**

```csharp
using MiasoftNanus.PhoneBook.Application.Abstractions.CQRS;

namespace MiasoftNanus.PhoneBook.Application.Abstractions.Behaviors;

/// <summary>
/// Defines a step that runs around a command handler (logging, validation, transactions, ...).
/// Behaviors are executed by the <see cref="Dispatcher"/> in registration order: the first registered behavior is
/// the outermost one.
/// </summary>
/// <typeparam name="TCommand">The type of the command. Must implement <see cref="ICommand{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the command.</typeparam>
public interface ICommandPipelineBehavior<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>
    /// Runs the behavior. Implementations must call <paramref name="next"/> to continue the pipeline, or return a
    /// result without calling it to short-circuit.
    /// </summary>
    /// <param name="command">The command being processed.</param>
    /// <param name="next">The next step of the pipeline.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task containing the result of the command.</returns>
    Task<TResult> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResult> next,
        CancellationToken cancellationToken = default);
}
```

3. **`Abstractions/Behaviors/LoggingBehavior.cs`**

```csharp
using System.Diagnostics;
using MiasoftNanus.PhoneBook.Application.Abstractions.CQRS;
using MiasoftNanus.PhoneBook.Domain.Abstractions;
using Microsoft.Extensions.Logging;

namespace MiasoftNanus.PhoneBook.Application.Abstractions.Behaviors;

/// <summary>
/// Logs the execution of every command: start, end with elapsed time, <see cref="Result"/> failures and exceptions.
/// Only the command type name is logged, never its data (commands can carry passwords and personal data).
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the command.</typeparam>
internal sealed partial class LoggingBehavior<TCommand, TResult>(ILogger<LoggingBehavior<TCommand, TResult>> logger)
    : ICommandPipelineBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<TResult> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResult> next,
        CancellationToken cancellationToken = default)
    {
        var commandName = typeof(TCommand).Name;
        var startTimestamp = Stopwatch.GetTimestamp();

        LogCommandExecuting(commandName);

        try
        {
            var response = await next();
            if (response is Result { IsFailure: true } failedResult)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    LogCommandFailed(commandName, failedResult.Error.Code,
                        Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
                }
            }
            else if (logger.IsEnabled(LogLevel.Information))
            {
                LogCommandExecuted(commandName, Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                LogCommandCancelled(commandName, Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
            }
            throw;
        }
        catch (Exception)
        {
            // Leave exception details to the global exception handler to avoid duplicate logging.
            if (logger.IsEnabled(LogLevel.Error))
            {
                LogCommandError(commandName, Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
            }
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Executing command {CommandName}")]
    private partial void LogCommandExecuting(string commandName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Command {CommandName} failed with error {ErrorCode} in {ElapsedMilliseconds:0.0} ms")]
    private partial void LogCommandFailed(string commandName, string errorCode, double elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Command {CommandName} executed in {ElapsedMilliseconds:0.0} ms")]
    private partial void LogCommandExecuted(string commandName, double elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Command {CommandName} cancelled after {ElapsedMilliseconds:0.0} ms")]
    private partial void LogCommandCancelled(string commandName, double elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error executing command {CommandName} after {ElapsedMilliseconds:0.0} ms")]
    private partial void LogCommandError(string commandName, double elapsedMilliseconds);
}
```

   - `ILogger<TCommand>` (as in the example): the log category is the command's full name, e.g.
     `MiasoftNanus.PhoneBook.Application.Agendas.UseCases.Commands.CreateAgenda.CreateAgendaCommand`, so levels can
     be tuned per feature with `Logging:LogLevel` prefixes.
   - `Result { IsFailure: true }` matches both `Result` and `Result<T>` (all command results today).
   - `throw;` keeps the stack trace; `GlobalExceptionHandler` still turns the exception into a 500 Problem Details.
   - `internal`: only `DependencyInjection` (same assembly) references it, like `Publisher`.

### Step 3 — `Abstractions/CQRS/Dispatcher.cs`

Only `CommandHandlerInvoker<TCommand, TResult>.InvokeCommandAsync` changes. The handler lookup and the
"no handler" exception stay as they are.

```csharp
using System.Collections.Concurrent;
using MiasoftNanus.PhoneBook.Application.Abstractions.Behaviors;
using Microsoft.Extensions.DependencyInjection;
```

```csharp
private sealed class CommandHandlerInvoker<TCommand, TResult> : ICommandHandlerInvoker<TResult>
    where TCommand : ICommand<TResult>
{
    public Task<TResult> InvokeCommandAsync(
        ICommand<TResult> command, IServiceProvider services, CancellationToken cancellationToken)
    {
        var handler = services.GetService(typeof(ICommandHandler<TCommand, TResult>))
                          as ICommandHandler<TCommand, TResult>
                      ?? throw InstantiateHandlerNotFoundException(typeof(TCommand), "command");

        var typedCommand = (TCommand)command;
        CommandHandlerDelegate<TResult> pipeline = () => handler.HandleAsync(typedCommand, cancellationToken);

        // Wrap from the last registered behavior to the first, so the first registered one runs outermost.
        var behaviors = services.GetServices<ICommandPipelineBehavior<TCommand, TResult>>().Reverse();
        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            pipeline = () => behavior.HandleAsync(typedCommand, next, cancellationToken);
        }

        return pipeline();
    }
}
```

   - With no behavior registered, `pipeline` is the handler call: same behavior as today.
   - `var next = pipeline;` is required: the lambda must capture the previous step, not the variable.
   - Update the XML summary of `Dispatcher.SendCommandAsync` / `CommandHandlerInvoker`: "...runs the registered
     `ICommandPipelineBehavior` instances around the handler."
   - `QueryHandlerInvoker` is not changed.

### Step 4 — `DependencyInjection.cs`

Register the behavior right after the dispatcher, before the handler scan:

```csharp
using MiasoftNanus.PhoneBook.Application.Abstractions.Behaviors;
```

```csharp
services.AddScoped<IDispatcher, Dispatcher>();
services.AddScoped<IPublisher, Publisher>();

// Register command pipeline behaviors. Order matters: the first registered behavior is the outermost one.
services.AddScoped(typeof(ICommandPipelineBehavior<,>), typeof(LoggingBehavior<,>));
```

   - Scoped, same lifetime as the handlers (a future behavior may need `IUnitOfWork`).
   - The handler scan is not affected: `LoggingBehavior<,>` is a generic type definition (filtered out by
     `IsGenericTypeDefinition: false`) and its interface is not in `handlerInterfaces`.

### Step 5 — Verify

- `dotnet build` the solution (only the existing `Microsoft.OpenApi` NU1903 warning expected).
- Run the WebApi and check the console and `bin/.../logs/logYYYYMMDD.txt`:
  - `POST /locations` (valid) → `Executing command CreateLocationCommand` + `Command CreateLocationCommand executed
    in ... ms`;
  - `DELETE /agendas/{unknown-id}` (with `X-Api-Key`) → `Executing ...` + **Warning** `... failed with error
    AgendaErrors.AgendaNotFound ...`, response still 404;
  - an exception inside a handler (e.g. stop the database and `POST /profiles`) → **Error** with the exception and
    command name, followed by the `GlobalExceptionHandler` log; response still 500 Problem Details;
  - `POST /users` / `PUT /users/{id}/password` → the log lines contain the command name only, **no password**;
  - `GET /agendas` → no behavior log (queries are not wrapped).
- Regression: every endpoint returns the same status codes and bodies as before (the behavior only observes).

## Scope

**In scope**
- Application: `Microsoft.Extensions.Logging.Abstractions` package, `Abstractions/Behaviors/`
  (`CommandHandlerDelegate`, `ICommandPipelineBehavior`, `LoggingBehavior`), pipeline execution in
  `Dispatcher.CommandHandlerInvoker`, registration in `AddApplication`.

**Out of scope**
- MediatR (not added) and FluentValidation (not used; see Notes).
- `ValidationBehavior` or any other behavior.
- Query pipeline (`IQueryPipelineBehavior`) and logging of queries.
- Logging of domain notifications (`Publisher`).
- Changes to Serilog configuration (`AppLoggingBuilderExtensions`), `GlobalExceptionHandler`, endpoints or DTOs.
- Unit / integration tests (no test projects yet).

## Notes / Future Considerations

- **FluentValidation.** The package is referenced in `MiasoftNanus.PhoneBook.Application.csproj` but no code uses it.
  This ticket does not touch it. Either remove the reference, or use it in a future `ValidationBehavior`
  (another `ICommandPipelineBehavior` registered **after** `LoggingBehavior`, so validation failures are logged too).
- **Double log on exceptions.** An unhandled exception is logged by `LoggingBehavior` (with the command name) and
  again by `GlobalExceptionHandler` (with method and path). Accepted for now: each line has context the other lacks.
  If it becomes noise, drop the stack trace from one of them, or add the command name to a log scope
  (`logger.BeginScope`) and log only in the handler.
- **Query pipeline.** Same pattern if needed: `QueryHandlerDelegate<TResult>`, `IQueryPipelineBehavior<,>`, the same
  wrapping loop in `QueryHandlerInvoker`, and a `QueryLoggingBehavior` probably at `Debug` level.
- **Log volume.** Two Information lines per command. To quiet them, add a level for the Application commands, e.g.
  `"MiasoftNanus.PhoneBook.Application": "Warning"` under `Logging:LogLevel` (keeps failures and errors).
- **High-performance logging.** If needed, move the messages to `[LoggerMessage]` source-generated methods
  (CA1848); the generator ships with `Microsoft.Extensions.Logging.Abstractions`.
- **Correlation.** Request id / trace id are not added to the logs here; `Serilog.AspNetCore` request logging or
  enrichers can add them later.
- Suggested commits: one for Steps 1–2 (package + behaviors), one for Steps 3–4 (dispatcher + DI).
