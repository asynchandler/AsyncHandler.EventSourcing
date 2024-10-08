﻿using System.Reflection;
using EventStorage.AggregateRoot;
using EventStorage.Configurations;
using EventStorage.Projections;
using EventStorage.Repositories;
using EventStorage.Schema;
using EventStorage.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TDiscover;

namespace EventStorage;

public static class EventSourceExtensions
{
    public static EventSourceConfiguration SelectEventSource(
        this EventSourceConfiguration configuration,
        EventSources source,
        string connectionString)
    {
        Type? aggregateType = Td.FindByCallingAsse<IAggregateRoot>(Assembly.GetCallingAssembly());
        if (aggregateType == null)
            return configuration;
        configuration.ServiceCollection.AddEventSourceSchema(configuration.Schema);
        // initialize source when app spins up
        configuration.ServiceCollection.AddSingleton<IHostedService>((sp) =>
        {
            var repository = new Repository<IAggregateRoot>(connectionString, sp, source);
            return new SourceInitializer(new EventSource<IAggregateRoot>(repository, source));
        });
        #pragma warning disable CS8603
        // register repository
        var repositoryInterfaceType = typeof(IRepository<>).MakeGenericType(aggregateType);
        var repositoryType = typeof(Repository<>).MakeGenericType(aggregateType);
        configuration.ServiceCollection.AddScoped(repositoryInterfaceType, sp =>
        {
            return Activator.CreateInstance(repositoryType, connectionString, sp, source);
        });
        // register event source
        Type eventSourceInterfaceType = typeof(IEventSource<>).MakeGenericType(aggregateType);
        Type eventSourceType = typeof(EventSource<>).MakeGenericType(aggregateType);
        configuration.ServiceCollection.AddScoped(eventSourceInterfaceType, sp =>
        {
            var repository = sp.GetService(repositoryInterfaceType);
            return Activator.CreateInstance(eventSourceType, repository, source);
        });
        return configuration;
    }
    public static EventSourceConfiguration AddProjection<T>(
        this EventSourceConfiguration configuration,
        ProjectionMode projectionMode)
    {
        return configuration;
    }
    private static IServiceCollection AddEventSourceSchema(this IServiceCollection services, string schema)
    {
        Dictionary<EventSources, IEventSourceSchema> schemas = [];
        schemas.Add(EventSources.AzureSql, new AzureSqlSchema(schema));
        schemas.Add(EventSources.PostgresSql, new PostgreSqlSchema(schema));
        schemas.Add(EventSources.SqlServer, new SqlServerSchema(schema));
        services.AddKeyedSingleton("Schema", schemas);
        return services;
    }
}
