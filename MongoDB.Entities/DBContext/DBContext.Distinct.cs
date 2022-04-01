﻿using MongoDB.Driver;

namespace MongoDB.Entities;

public partial class DBContext
{
    /// <summary>
    /// Represents a MongoDB Distinct command where you can get back distinct values for a given property of a given Entity
    /// </summary>
    /// <typeparam name="T">Any Entity that implements IEntity interface</typeparam>
    /// <typeparam name="TId">Id type</typeparam>
    /// <typeparam name="TProperty">The type of the property of the entity you'd like to get unique values for</typeparam>
    public Distinct<T, TProperty> Distinct<T, TId, TProperty>(string? collectionName = null, IMongoCollection<T>? collection = null)
    {
        return new Distinct<T, TProperty>(this, Collection(collectionName, collection));
    }
}
