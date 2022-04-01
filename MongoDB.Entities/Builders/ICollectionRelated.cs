﻿namespace MongoDB.Entities;

internal interface ICollectionRelated<T>
{
    public DBContext Context { get; }
    public IMongoCollection<T> Collection { get; }
}
internal static class ICollectionRelatedExt
{
    public static EntityCache<T> Cache<T>(this ICollectionRelated<T> c) => c.Context.Cache<T>();
    public static IClientSessionHandle? Session<T>(this ICollectionRelated<T> collectionRelated)
    {
        return collectionRelated.Context.Session;
    }

}
