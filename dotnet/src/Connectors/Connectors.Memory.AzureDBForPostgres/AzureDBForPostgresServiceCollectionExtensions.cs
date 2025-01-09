// Copyright (c) Microsoft. All rights reserved.

using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.AzureDBForPostgres;
using Microsoft.SemanticKernel.Connectors.Postgres;
using Npgsql;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Extension methods to register an Azure DB for Postgres <see cref="IVectorStore"/> instance with Entra authentication on an <see cref="IServiceCollection"/>.
/// </summary>
public static class AzureDBForPostgresServiceCollectionExtensions
{
    /// <summary>
    /// Register an Postgres <see cref="IVectorStore"/> for Azure DB for Postgres with the specified service ID and where an NpgsqlDataSource is constructed using the provided parameters and Entra authentication.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="IVectorStore"/> on.</param>
    /// <param name="connectionString">Postgres database connection string.</param>
    /// <param name="entraCredential">The credential to use for Entra authentication. Defaults to <see cref="DefaultAzureCredential"/></param>
    /// <param name="options">Optional options to further configure the <see cref="IVectorStore"/>.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPostgresVectorStore(
        this IServiceCollection services,
        string connectionString,
        TokenCredential? entraCredential = default,
        PostgresVectorStoreOptions? options = default,
        string? serviceId = default)
    {
        string? npgsqlServiceId = serviceId == null ? default : $"{serviceId}_NpgsqlDataSource";

        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString);
        dataSourceBuilder.UseEntraAuthentication(entraCredential);
        dataSourceBuilder.UseVector();

        // Register NpgsqlDataSource to ensure proper disposal.
        services.AddKeyedSingleton<NpgsqlDataSource>(
            npgsqlServiceId,
            (sp, obj) =>
            {
                return dataSourceBuilder.Build();
            });

        services.AddKeyedSingleton<IVectorStore>(
            serviceId,
            (sp, obj) =>
            {
                var dataSource = sp.GetRequiredKeyedService<NpgsqlDataSource>(npgsqlServiceId);
                var selectedOptions = options ?? sp.GetService<PostgresVectorStoreOptions>();

                return new PostgresVectorStore(
                    dataSource,
                    selectedOptions);
            });

        return services;
    }

    /// <summary>
    /// Register a Postgres <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> and <see cref="IVectorizedSearch{TRecord}"/> with the specified service ID
    /// and where the NpgsqlDataSource is constructed using the provided parameters.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TRecord">The type of the record.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> on.</param>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="connectionString">Postgres database connection string.</param>
    /// <param name="entraCredential">The credential to use for Entra authentication. Defaults to <see cref="DefaultAzureCredential"/></param>
    /// <param name="options">Optional options to further configure the <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/>.</param>
    /// <param name="serviceId">An optional service id to use as the service key.</param>
    /// <returns>Service collection.</returns>
    public static IServiceCollection AddPostgresVectorStoreRecordCollectionAsync<TKey, TRecord>(
        this IServiceCollection services,
        string collectionName,
        string connectionString,
        TokenCredential? entraCredential = default,
        PostgresVectorStoreRecordCollectionOptions<TRecord>? options = default,
        string? serviceId = default)
        where TKey : notnull
    {
        string? npgsqlServiceId = serviceId == null ? default : $"{serviceId}_NpgsqlDataSource";

        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString);
        dataSourceBuilder.UseEntraAuthentication(entraCredential);
        dataSourceBuilder.UseVector();

        // Register NpgsqlDataSource to ensure proper disposal.
        services.AddKeyedSingleton<NpgsqlDataSource>(
            npgsqlServiceId,
            (sp, obj) => dataSourceBuilder.Build()
        );

        services.AddKeyedSingleton<IVectorStoreRecordCollection<TKey, TRecord>>(
            serviceId,
            (sp, obj) =>
            {
                var dataSource = sp.GetRequiredKeyedService<NpgsqlDataSource>(npgsqlServiceId);

                return (new PostgresVectorStoreRecordCollection<TKey, TRecord>(dataSource, collectionName, options) as IVectorStoreRecordCollection<TKey, TRecord>)!;
            });

        // Also register the record collection G with the given <paramref name="serviceId"/> as a <see cref="IVectorizedSearch{TRecord}"/>.
        services.AddKeyedTransient<IVectorizedSearch<TRecord>>(
            serviceId,
            (sp, obj) =>
            {
                return sp.GetRequiredKeyedService<IVectorStoreRecordCollection<TKey, TRecord>>(serviceId);
            });

        return services;
    }
}
