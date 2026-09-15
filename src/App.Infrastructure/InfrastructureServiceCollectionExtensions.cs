using App.Application.Common;
using App.Domain.Common;
using App.Domain.Person;
using App.Domain.ReferenceData;
using App.Domain.Sample;
using App.Infrastructure.Messaging;
using App.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Registers the DbContext, the SQL migrator, repositories and the outbox-backed unit of work.</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => PersistenceOptions.Configure(options, connectionString));
        services.AddSingleton(sp => new SqlMigrator(connectionString, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<SqlMigrator>>()));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ISamples, Samples>();
        services.AddScoped<IPeople, People>();
        services.AddScoped<ICities, Cities>();

        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// Registers the outbox dispatcher and every <see cref="IEventHandler{TEvent}"/> found in the application ring.
    /// Discovery by assembly scan: adding a handler needs no registration (design §12, option A).
    /// </summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<OutboxSignal>();
        services.AddHostedService<OutboxDispatcher>();

        var handlerTypes = typeof(IEventHandler<>).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                .Select(i => (Service: i, Implementation: t)));

        foreach (var (service, implementation) in handlerTypes)
        {
            services.AddScoped(service, implementation);
        }

        return services;
    }
}
