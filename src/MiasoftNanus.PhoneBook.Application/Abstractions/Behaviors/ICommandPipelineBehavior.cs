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
