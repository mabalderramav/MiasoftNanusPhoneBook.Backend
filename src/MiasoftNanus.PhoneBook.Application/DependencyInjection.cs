using MiasoftNanus.PhoneBook.Application.Abstractions.Behaviors;
using MiasoftNanus.PhoneBook.Application.Abstractions.CQRS;
using MiasoftNanus.PhoneBook.Application.Abstractions.Notifications;
using MiasoftNanus.PhoneBook.Application.Agendas.Services;
using MiasoftNanus.PhoneBook.Application.Locations.Services;
using MiasoftNanus.PhoneBook.Application.Profiles.Services;
using MiasoftNanus.PhoneBook.Application.Users.Services;
using MiasoftNanus.PhoneBook.Application.WorkAreas.Services;
using MiasoftNanus.PhoneBook.Application.Workstations.Services;
using MiasoftNanus.PhoneBook.Domain.Abstractions;
using MiasoftNanus.PhoneBook.Domain.Agendas.Services;
using MiasoftNanus.PhoneBook.Domain.Locations.Services;
using MiasoftNanus.PhoneBook.Domain.Profiles.Services;
using MiasoftNanus.PhoneBook.Domain.Users.Services;
using MiasoftNanus.PhoneBook.Domain.WorkAreas.Services;
using MiasoftNanus.PhoneBook.Domain.Workstations.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MiasoftNanus.PhoneBook.Application;

/// <summary>
/// Provides extension methods for configuring application-level dependency injection.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IPublisher, Publisher>();

        // Register command pipeline behaviors. Order matters: the first registered behavior is the outermost one.
        services.AddScoped(typeof(ICommandPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        // Register application services.
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IWorkAreaService, WorkAreaService>();
        services.AddScoped<IWorkstationService, WorkstationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAgendaService, AgendaService>();

        // Register all command, query, and notification handlers dynamically.
        var handlerInterfaces = new[]
        {
            typeof(ICommandHandler<,>),
            typeof(IQueryHandler<,>),
            typeof(INotificationHandler<>)
        };

        // Find all concrete classes that implement one of the supported handler interfaces.
        var registeredHandlers =
            from typeInfo in typeof(DependencyInjection).Assembly.GetTypes()
            where typeInfo is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
            from implementedInterface in typeInfo.GetInterfaces()
            where implementedInterface.IsGenericType
                  && handlerInterfaces.Contains(implementedInterface.GetGenericTypeDefinition())
            select (implementedInterface, typeInfo);

        // Register each handler interface and its corresponding implementation type with the DI container.
        foreach (var (handlerInterface, implementationType) in registeredHandlers)
        {
            services.AddScoped(handlerInterface, implementationType);
        }

        return services;
    }
}
