// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.VectorData;
using Pgvector;

namespace Microsoft.SemanticKernel.Connectors.Postgres;

internal sealed class PostgresGenericDataModelMapper : IVectorStoreRecordMapper<VectorStoreGenericDataModel<ulong>, Dictionary<string, object?>>,
    IVectorStoreRecordMapper<VectorStoreGenericDataModel<string>, Dictionary<string, object?>>
{
    /// <summary><see cref="VectorStoreRecordPropertyReader"/> with helpers for reading vector store model properties and their attributes.</summary>
    private readonly VectorStoreRecordPropertyReader _propertyReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresGenericDataModelMapper"/> class.
    /// </summary>
    /// <param name="propertyReader">A <see cref="VectorStoreRecordDefinition"/> that defines the schema of the data in the database.</param>
    public PostgresGenericDataModelMapper(VectorStoreRecordPropertyReader propertyReader)
    {
        Verify.NotNull(propertyReader);

        this._propertyReader = propertyReader;

        // Validate property types.
        this._propertyReader.VerifyDataProperties(PostgresConstants.SupportedDataTypes, supportEnumerable: false);
        this._propertyReader.VerifyVectorProperties(PostgresConstants.SupportedVectorTypes);
    }
    public Dictionary<string, object?> MapFromDataToStorageModel(VectorStoreGenericDataModel<ulong> dataModel)
    {
        return this.InternalMapFromDataToStorageModel(dataModel);
    }

    VectorStoreGenericDataModel<string> IVectorStoreRecordMapper<VectorStoreGenericDataModel<string>, Dictionary<string, object?>>.MapFromStorageToDataModel(Dictionary<string, object?> storageModel, StorageToDataModelMapperOptions options)
    {
        return this.InternalMapFromStorageToDataModel<string>(storageModel, options);
    }

    public Dictionary<string, object?> MapFromDataToStorageModel(VectorStoreGenericDataModel<string> dataModel)
    {
        return this.InternalMapFromDataToStorageModel(dataModel);
    }

    VectorStoreGenericDataModel<ulong> IVectorStoreRecordMapper<VectorStoreGenericDataModel<ulong>, Dictionary<string, object?>>.MapFromStorageToDataModel(Dictionary<string, object?> storageModel, StorageToDataModelMapperOptions options)
    {
        return this.InternalMapFromStorageToDataModel<ulong>(storageModel, options);
    }

    private Dictionary<string, object?> InternalMapFromDataToStorageModel<TKey>(VectorStoreGenericDataModel<TKey> dataModel)
        where TKey : notnull
    {
        var properties = new Dictionary<string, object?>
        {
            // Add key property
            { this._propertyReader.KeyPropertyStoragePropertyName, dataModel.Key }
        };

        // Add data properties
        if (dataModel.Data is not null)
        {
            foreach (var property in this._propertyReader.DataProperties)
            {
                if (dataModel.Data.TryGetValue(property.DataModelPropertyName, out var dataValue))
                {
                    properties.Add(this._propertyReader.GetStoragePropertyName(property.DataModelPropertyName), dataValue);
                }
            }
        }

        // Add vector properties
        if (dataModel.Vectors is not null)
        {
            foreach (var property in this._propertyReader.VectorProperties)
            {
                if (dataModel.Vectors.TryGetValue(property.DataModelPropertyName, out var vectorValue))
                {
                    object? result = null;

                    if (vectorValue is not null)
                    {
                        var vector = (ReadOnlyMemory<float>)vectorValue;
                        result = new Vector(PostgresVectorStoreRecordPropertyMapping.GetOrCreateArray(vector));
                    }

                    properties.Add(this._propertyReader.GetStoragePropertyName(property.DataModelPropertyName), result);
                }
            }
        }

        return properties;
    }

    private VectorStoreGenericDataModel<TKey> InternalMapFromStorageToDataModel<TKey>(Dictionary<string, object?> storageModel, StorageToDataModelMapperOptions options)
        where TKey : notnull
    {
        TKey key;
        var dataProperties = new Dictionary<string, object?>();
        var vectorProperties = new Dictionary<string, object?>();

        // Process key property.
        if (storageModel.TryGetValue(this._propertyReader.KeyPropertyStoragePropertyName, out var keyObject) && keyObject is not null)
        {
            key = (TKey)keyObject;
        }
        else
        {
            throw new VectorStoreRecordMappingException("No key property was found in the record retrieved from storage.");
        }

        // Process data properties.
        foreach (var property in this._propertyReader.DataProperties)
        {
            if (storageModel.TryGetValue(this._propertyReader.GetStoragePropertyName(property.DataModelPropertyName), out var dataValue))
            {
                dataProperties.Add(property.DataModelPropertyName, dataValue);
            }
        }

        // Process vector properties
        if (options.IncludeVectors)
        {
            foreach (var property in this._propertyReader.VectorProperties)
            {
                if (storageModel.TryGetValue(this._propertyReader.GetStoragePropertyName(property.DataModelPropertyName), out var vectorValue))
                {
                    if (vectorValue is null)
                    {
                        vectorProperties.Add(property.DataModelPropertyName, ReadOnlyMemory<float>.Empty);
                    }
                    else if (vectorValue is Vector pgVector)
                    {
                        vectorProperties.Add(property.DataModelPropertyName, pgVector.ToArray());
                    }
                }
            }
        }

        return new VectorStoreGenericDataModel<TKey>(key) { Data = dataProperties, Vectors = vectorProperties };
    }
}