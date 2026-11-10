using System.Collections.Concurrent;
using MiasoftNanus.PhoneBook.Application.Abstractions.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace MiasoftNanus.PhoneBook.Application.Abstractions.CQRS;

/// <summary>
/// The Dispatcher is a sealed class responsible for dispatching commands and queries
/// to their respective handlers as part of the CQRS pattern. It serves as the central
/// mechanism for executing command and query operations within the application.
/// </summary>
public sealed class Dispatcher(IServiceProvider servicesProvider) : IDispatcher
{
    private static readonly ConcurrentDictionary<(Type, Type), object> CommandInvokerCache = new();
    private static readonly ConcurrentDictionary<(Type, Type), object> QueryInvokerCache = new();

    /// <summary>
    /// Asynchronously sends a command to its corresponding handler for processing
    /// and returns the result. The registered <see cref="ICommandPipelineBehavior{TCommand, TResult}"/>
    /// instances run around the handler. This method is a core part of the CQRS pattern
    /// implementation within the application.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the command handler.</typeparam>
    /// <param name="command">
    /// The command to be processed. The command must implement <see cref="ICommand{TResult}"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// An optional token to monitor for request cancellation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the
    /// result of the command handler's execution.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the provided <paramref name="command"/> is null.
    /// </exception>
    public Task<TResult> SendCommandAsync<TResult>(
        ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invoker = (ICommandHandlerInvoker<TResult>)CommandInvokerCache.GetOrAdd(
            (command.GetType(), typeof(TResult)),
            clave => Activator.CreateInstance(
                typeof(CommandHandlerInvoker<,>).MakeGenericType(clave.Item1, clave.Item2))!);

        return invoker.InvokeCommandAsync(command, servicesProvider, cancellationToken);
    }

    /// <summary>
    /// Asynchronously sends a query to its corresponding handler for processing
    /// and returns the result. This method is a part of the CQRS pattern implementation,
    /// enabling query operations in the application.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the query handler.</typeparam>
    /// <param name="query">
    /// The query to be processed. The query must implement <see cref="IQuery{TResult}"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// An optional token to monitor for request cancellation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains the
    /// result of the query handler's execution.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the provided <paramref name="query"/> is null.
    /// </exception>
    public Task<TResult> SendQueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var invoker = (IQueryHandlerInvoker<TResult>)QueryInvokerCache.GetOrAdd(
            (query.GetType(), typeof(TResult)),
            clave => Activator.CreateInstance(
                typeof(QueryHandlerInvoker<,>).MakeGenericType(clave.Item1, clave.Item2))!);

        return invoker.InvokeQueryAsync(query, servicesProvider, cancellationToken);
    }

    /// <summary>
    /// Represents an abstraction for invoking command handlers in the CQRS pattern.
    /// Provides a mechanism to execute commands asynchronously by delegating execution
    /// to the appropriate handler. This interface is designed to be implemented for handling
    /// the interaction between commands and their registered handlers within the application.
    /// </summary>
    /// <typeparam name="TResult">
    /// The type of the result expected from the execution of the command.
    /// </typeparam>
    private interface ICommandHandlerInvoker<TResult>
    {
        /// <summary>
        /// Asynchronously invokes a command using the appropriate handler and returns the result.
        /// This method is designed to work as part of the CQRS pattern implementation.
        /// </summary>
        /// <typeparam name="TResult">The type of the result expected from the command execution.</typeparam>
        /// <param name="command">
        /// The command to invoke. This must be an implementation of <see cref="ICommand{TResult}"/>.
        /// </param>
        /// <param name="services">
        /// The service provider instance used to resolve dependencies required for command execution.
        /// </param>
        /// <param name="cancellationToken">
        /// An optional token to monitor for request cancellation. Defaults to <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous command invocation. The task result contains the outcome
        /// of the command execution.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the provided <paramref name="command"/> or <paramref name="services"/> is null.
        /// </exception>
        Task<TResult> InvokeCommandAsync(
            ICommand<TResult> command, IServiceProvider services, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The CommandHandlerInvoker is a private sealed class responsible for invoking
    /// the appropriate command handler to process a specific command. It ensures
    /// that commands are executed by their corresponding handlers in a decoupled
    /// manner, adhering to the CQRS pattern. The registered
    /// <see cref="ICommandPipelineBehavior{TCommand, TResult}"/> instances run around the handler,
    /// the first registered one being the outermost.
    /// </summary>
    /// <typeparam name="TCommand">
    /// The type of the command to be handled. It must implement the ICommand interface
    /// with an associated TResult return type.
    /// </typeparam>
    /// <typeparam name="TResult">
    /// The type of the result returned by the command handler after processing the command.
    /// </typeparam>
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

    /// <summary>
    /// Represents a contract for invoking query handlers in a CQRS architecture.
    /// This interface defines the mechanism for executing a query by using
    /// the appropriate query handler, enabling separation of query logic and execution.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the query handler.</typeparam>
    private interface IQueryHandlerInvoker<TResult>
    {
        /// <summary>
        /// Asynchronously invokes the appropriate query handler to process the given query
        /// and returns the result. This method is a fundamental part of implementing the
        /// query handling side of the CQRS pattern in the application.
        /// </summary>
        /// <typeparam name="TResult">The type of the result returned by the query handler.</typeparam>
        /// <param name="query">
        /// The query to be processed. The query must implement <see cref="IQuery{TResult}"/>.
        /// </param>
        /// <param name="services">
        /// The service provider used to resolve the dependencies required by the query handler.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous query execution operation. The result of the task
        /// contains the data returned by the query handler.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the provided <paramref name="query"/> or <paramref name="services"/> is null.
        /// </exception>
        Task<TResult> InvokeQueryAsync(
            IQuery<TResult> query, IServiceProvider services, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The QueryHandlerInvoker is a sealed class responsible for dynamically invoking query handlers
    /// within the CQRS pattern. This class ensures that the appropriate query handler is retrieved
    /// and executed to process a given query and return its result.
    /// </summary>
    /// <typeparam name="TQuery">The type of the query being processed.</typeparam>
    /// <typeparam name="TResult">The type of the result returned by the query handler.</typeparam>
    private sealed class QueryHandlerInvoker<TQuery, TResult> : IQueryHandlerInvoker<TResult>
        where TQuery : IQuery<TResult>
    {
        public Task<TResult> InvokeQueryAsync(
            IQuery<TResult> query, IServiceProvider services, CancellationToken cancellationToken)
        {
            var handler = services.GetService(typeof(IQueryHandler<TQuery, TResult>))
                              as IQueryHandler<TQuery, TResult>
                          ?? throw InstantiateHandlerNotFoundException(typeof(TQuery), "query");

            return handler.HandleAsync((TQuery)query, cancellationToken);
        }
    }

    /// <summary>
    /// Creates an <see cref="InvalidOperationException"/> to indicate that no handler
    /// was found for a specified command or query type within the application.
    /// This exception is typically thrown when attempting to invoke a handler
    /// for a command or query that is not registered in the service provider.
    /// </summary>
    /// <param name="message">
    /// The type of the command or query for which no handler was found.
    /// </param>
    /// <param name="kind">
    /// A string representing the kind of handler being sought, such as "command" or "query".
    /// </param>
    /// <returns>
    /// An instance of <see cref="InvalidOperationException"/> describing the absence
    /// of the required handler for the specified type.
    /// </returns>
    private static InvalidOperationException InstantiateHandlerNotFoundException(Type message, string kind) =>
        new($"No handler registered for {kind} '{message.Name}'. " +
            "Ensure there is a class that handles it and that AddApplication() has been called on the host.");
}