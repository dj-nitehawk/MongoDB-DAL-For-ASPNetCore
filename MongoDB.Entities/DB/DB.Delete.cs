﻿using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace MongoDB.Entities;

public static partial class DB
{
    private static readonly int deleteBatchSize = 100000;

    private static async Task<DeleteResult> DeleteCascadingAsync<T>(
        IEnumerable<object?> IDs,
        IClientSessionHandle? session = null,
        CancellationToken cancellation = default) where T : IEntity
    {
        // note: cancellation should not be enabled outside of transactions because multiple collections are involved 
        //       and premature cancellation could cause data inconsistencies.
        //       i.e. don't pass the cancellation token to delete methods below that don't take a session.
        //       also make consumers call ThrowIfCancellationNotSupported() before calling this method.

        var db = Database<T>();
        var options = new ListCollectionNamesOptions
        {
            Filter = "{$and:[{name:/~/},{name:/" + CollectionName<T>() + "/}]}"
        };

        var tasks = new List<Task>();

        // note: db.listCollections() mongo command does not support transactions.
        //       so don't add session support here.
        var collNamesCursor = await db.ListCollectionNamesAsync(options, cancellation).ConfigureAwait(false);

        foreach (var cName in await collNamesCursor.ToListAsync(cancellation).ConfigureAwait(false))
        {
            tasks.Add(
                session == null
                ? db.GetCollection<JoinRecord>(cName).DeleteManyAsync(r => IDs.Contains(r.ChildID) || IDs.Contains(r.ParentID))
                : db.GetCollection<JoinRecord>(cName).DeleteManyAsync(session, r => IDs.Contains(r.ChildID) || IDs.Contains(r.ParentID), null, cancellation));
        }

        var filter = Builders<T>.Filter.In(Cache<T>.IdPropName, IDs);

        var delResTask =
                session == null
                ? Collection<T>().DeleteManyAsync(filter)
                : Collection<T>().DeleteManyAsync(session, filter, null, cancellation);

        tasks.Add(delResTask);

        if (typeof(T).BaseType == typeof(FileEntity))
        {
            tasks.Add(
                session == null
                ? db.GetCollection<FileChunk>(CollectionName<FileChunk>()).DeleteManyAsync(x => IDs.Contains(x.FileID))
                : db.GetCollection<FileChunk>(CollectionName<FileChunk>()).DeleteManyAsync(session, x => IDs.Contains(x.FileID), null, cancellation));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return await delResTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a single entity from MongoDB.
    /// <para>HINT: If this entity is referenced by one-to-many/many-to-many relationships, those references are also deleted.</para>
    /// </summary>
    /// <typeparam name="T">Any class that implements IEntity</typeparam>
    /// <param name="ID">The Id of the entity to delete</param>
    /// <param name = "session" >An optional session if using within a transaction</param>
    /// <param name="cancellation">An optional cancellation token</param>
    public static Task<DeleteResult> DeleteAsync<T>(object? ID, IClientSessionHandle? session = null, CancellationToken cancellation = default) where T : IEntity
    {
        ThrowIfCancellationNotSupported(session, cancellation);
        return DeleteCascadingAsync<T>(new[] { ID }, session, cancellation);
    }

    /// <summary>
    /// Deletes entities using a collection of IDs
    /// <para>HINT: If more than 100,000 IDs are passed in, they will be processed in batches of 100k.</para>
    /// <para>HINT: If these entities are referenced by one-to-many/many-to-many relationships, those references are also deleted.</para>
    /// </summary>
    /// <typeparam name="T">Any class that implements IEntity</typeparam>
    /// <param name="IDs">An IEnumerable of entity IDs</param>
    /// <param name = "session" > An optional session if using within a transaction</param>
    /// <param name="cancellation">An optional cancellation token</param>
    public static async Task<DeleteResult> DeleteAsync<T>(IEnumerable<object?> IDs, IClientSessionHandle? session = null, CancellationToken cancellation = default) where T : IEntity
    {
        ThrowIfCancellationNotSupported(session, cancellation);

        if (IDs.Count() <= deleteBatchSize)
            return await DeleteCascadingAsync<T>(IDs, session, cancellation).ConfigureAwait(false);

        long deletedCount = 0;
        DeleteResult res = DeleteResult.Unacknowledged.Instance;

        foreach (var batch in IDs.ToBatches(deleteBatchSize))
        {
            res = await DeleteCascadingAsync<T>(batch, session, cancellation).ConfigureAwait(false);
            deletedCount += res.DeletedCount;
        }

        if (res.IsAcknowledged)
        {
            res = new DeleteResult.Acknowledged(deletedCount);
        }

        return res;
    }

    /// <summary>
    /// Deletes matching entities with an expression
    /// <para>HINT: If the expression matches more than 100,000 entities, they will be deleted in batches of 100k.</para>
    /// <para>HINT: If these entities are referenced by one-to-many/many-to-many relationships, those references are also deleted.</para>
    /// </summary>
    /// <typeparam name="T">Any class that implements IEntity</typeparam>
    /// <param name="expression">A lambda expression for matching entities to delete.</param>
    /// <param name = "session" >An optional session if using within a transaction</param>
    /// <param name="cancellation">An optional cancellation token</param>
    /// <param name="collation">An optional collation object</param>
    public static Task<DeleteResult> DeleteAsync<T>(Expression<Func<T, bool>> expression, IClientSessionHandle? session = null, CancellationToken cancellation = default, Collation? collation = null) where T : IEntity
    {
        return DeleteAsync(Builders<T>.Filter.Where(expression), session, cancellation, collation);
    }

    /// <summary>
    /// Deletes matching entities with a filter expression
    /// <para>HINT: If the expression matches more than 100,000 entities, they will be deleted in batches of 100k.</para>
    /// <para>HINT: If these entities are referenced by one-to-many/many-to-many relationships, those references are also deleted.</para>
    /// </summary>
    /// <typeparam name="T">Any class that implements IEntity</typeparam>
    /// <param name="filter">f => f.Eq(x => x.Prop, Value) &amp; f.Gt(x => x.Prop, Value)</param>
    /// <param name = "session" >An optional session if using within a transaction</param>
    /// <param name="cancellation">An optional cancellation token</param>
    /// <param name="collation">An optional collation object</param>
    public static Task<DeleteResult> DeleteAsync<T>(Func<FilterDefinitionBuilder<T>, FilterDefinition<T>> filter, IClientSessionHandle? session = null, CancellationToken cancellation = default, Collation? collation = null) where T : IEntity
    {
        return DeleteAsync(filter(Builders<T>.Filter), session, cancellation, collation);
    }

    /// <summary>
    /// Deletes matching entities with a filter definition
    /// <para>HINT: If the expression matches more than 100,000 entities, they will be deleted in batches of 100k.</para>
    /// <para>HINT: If these entities are referenced by one-to-many/many-to-many relationships, those references are also deleted.</para>
    /// </summary>
    /// <typeparam name="T">Any class that implements IEntity</typeparam>
    /// <param name="filter">A filter definition for matching entities to delete.</param>
    /// <param name = "session" >An optional session if using within a transaction</param>
    /// <param name="cancellation">An optional cancellation token</param>
    /// <param name="collation">An optional collation object</param>
    public static async Task<DeleteResult> DeleteAsync<T>(FilterDefinition<T> filter, IClientSessionHandle? session = null, CancellationToken cancellation = default, Collation? collation = null) where T : IEntity
    {
        ThrowIfCancellationNotSupported(session, cancellation);

        //workaround for the newly added implicit operator in driver which matches all strings as json filters
        var jsonFilter = filter as JsonFilterDefinition<T>;
        if (jsonFilter?.Json.StartsWith("{") is false)
            filter = Builders<T>.Filter.Eq(Cache<T>.IdExpression, jsonFilter.Json);

        var cursor = await new Find<T, object?>(session, null)
                           .Match(_ => filter)
                           .Project(p => p.Include(Cache<T>.IdPropName))
                           .Option(o => o.BatchSize = deleteBatchSize)
                           .Option(o => o.Collation = collation)
                           .ExecuteCursorAsync(cancellation)
                           .ConfigureAwait(false);

        long deletedCount = 0;
        DeleteResult res = DeleteResult.Unacknowledged.Instance;

        using (cursor)
        {
            while (await cursor.MoveNextAsync(cancellation).ConfigureAwait(false))
            {
                if (cursor.Current.Any())
                {
                    var idObjects = ValidateCursor((List<object>)cursor.Current);
                    res = await DeleteCascadingAsync<T>(idObjects, session, cancellation).ConfigureAwait(false);
                    deletedCount += res.DeletedCount;
                }
            }
        }

        if (res.IsAcknowledged)
        {
            res = new DeleteResult.Acknowledged(deletedCount);
        }

        return res;
    }

    private static List<object> ValidateCursor(List<object> idObjects)
    {
        if (idObjects.Any() && idObjects[0] is ExpandoObject)
        {
            List<object> ids = new();
            foreach (dynamic id in idObjects)
            {
                ids.Add(id._id);
            }
            return ids;
        }

        return idObjects;

    }

    private static void ThrowIfCancellationNotSupported(IClientSessionHandle? session = null, CancellationToken cancellation = default)
    {
        if (cancellation != default && session == null)
            throw new NotSupportedException("Cancellation is only supported within transactions for delete operations!");
    }
}
