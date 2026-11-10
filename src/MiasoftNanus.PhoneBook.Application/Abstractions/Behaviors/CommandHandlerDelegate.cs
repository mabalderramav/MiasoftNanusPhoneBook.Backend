namespace MiasoftNanus.PhoneBook.Application.Abstractions.Behaviors;

/// <summary>
/// Represents the next step of the command pipeline: the next behavior, or the command handler itself.
/// </summary>
/// <typeparam name="TResult">The type of the result produced by the command.</typeparam>
/// <returns>A task containing the result of the rest of the pipeline.</returns>
public delegate Task<TResult> CommandHandlerDelegate<TResult>();
