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
