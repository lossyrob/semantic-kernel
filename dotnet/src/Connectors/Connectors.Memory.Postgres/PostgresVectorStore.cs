// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;
using System.Threading.Tasks;
using Npgsql;

namespace Microsoft.SemanticKernel.Connectors.Postgres;

/// <summary>
/// Represents a vector store implementation using PostgreSQL.
/// </summary>
public class PostgresVectorStore : IVectorStore
{
    private readonly IPostgresVectorStoreDbClient _postgresClient;
    private readonly NpgsqlDataSource? _dataSource;
    private readonly PostgresVectorStoreOptions? _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresVectorStore"/> class.
    /// </summary>
    /// <param name="connectionString">Postgres database connection string.</param>
    /// <param name="options">Optional configuration options for this class</param>
    public PostgresVectorStore(string connectionString, PostgresVectorStoreOptions? options = default)
    {
        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString);
        dataSourceBuilder.UseVector();
        this._dataSource = dataSourceBuilder.Build();
        this._options = options ?? new PostgresVectorStoreOptions();
        this._postgresClient = new PostgresVectorStoreDbClient(this._dataSource, this._options.Schema);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresVectorStore"/> class.
    /// </summary>
    /// <param name="dataSource">Postgres data source.</param>
    /// <param name="options">Optional configuration options for this class</param>
    public PostgresVectorStore(NpgsqlDataSource dataSource, PostgresVectorStoreOptions? options = default)
    {
        this._dataSource = dataSource;
        this._options = options ?? new PostgresVectorStoreOptions();
        this._postgresClient = new PostgresVectorStoreDbClient(this._dataSource, this._options.Schema);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresVectorStore"/> class.
    /// </summary>
    /// <param name="postgresDbClient">An instance of <see cref="IPostgresDbClient"/>.</param>
    /// <param name="options">Optional configuration options for this class</param>
    public PostgresVectorStore(IPostgresVectorStoreDbClient postgresDbClient, PostgresVectorStoreOptions? options = default)
    {
        this._postgresClient = postgresDbClient;
        this._options = options ?? new PostgresVectorStoreOptions();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (string collection in this._postgresClient.GetTablesAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return collection;
        }
    }

    /// <inheritdoc />
    public IVectorStoreRecordCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null)
        where TKey : notnull
    {
        // Support int, long, Guid, and string keys
        if (typeof(TKey) != typeof(int) && typeof(TKey) != typeof(long) && typeof(TKey) != typeof(Guid) && typeof(TKey) != typeof(string))
        {
            throw new NotSupportedException($"Only int, long, {nameof(Guid)}, and {nameof(String)} keys are supported.");
        }

        if (this._options?.VectorStoreCollectionFactory is not null)
        {
            return this._options.VectorStoreCollectionFactory.CreateVectorStoreRecordCollection<TKey, TRecord>(this._postgresClient, name, vectorStoreRecordDefinition);
        }

        var recordCollection = new PostgresVectorStoreRecordCollection<TKey, TRecord>(
            this._postgresClient,
            name,
            new PostgresVectorStoreRecordCollectionOptions<TRecord>() { VectorStoreRecordDefinition = vectorStoreRecordDefinition }
        );

        return recordCollection as IVectorStoreRecordCollection<TKey, TRecord> ?? throw new InvalidOperationException("Failed to cast record collection.");
    }
}