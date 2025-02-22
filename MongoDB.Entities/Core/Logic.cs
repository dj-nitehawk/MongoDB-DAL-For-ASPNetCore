﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MongoDB.Entities;

static class Logic
{
    internal static IEnumerable<UpdateDefinition<T>> BuildUpdateDefs<T>(T entity) where T : IEntity
    {
        if (entity == null)
            throw new ArgumentException("The supplied entity cannot be null!");

        var props = Cache<T>.UpdatableProps(entity);

        return props.Select(p => Builders<T>.Update.Set(p.Name, p.GetValue(entity)));
    }

    internal static IEnumerable<string> GetPropNamesFromExpression<T>(Expression<Func<T, object?>> expression)
        => (expression.Body as NewExpression)?.Arguments.Select(a => a.ToString().Split('.')[1]) ?? [];

    internal static IEnumerable<UpdateDefinition<T>> BuildUpdateDefs<T>(T entity,
                                                                        Expression<Func<T, object?>> members,
                                                                        bool excludeMode = false) where T : IEntity
        => BuildUpdateDefs(entity, GetPropNamesFromExpression(members), excludeMode);

    internal static IEnumerable<UpdateDefinition<T>> BuildUpdateDefs<T>(T entity, IEnumerable<string> propNames, bool excludeMode = false)
        where T : IEntity
    {
        if (!propNames.Any())
            throw new ArgumentException("Unable to get any properties from the members expression!");

        var props = Cache<T>.UpdatableProps(entity);

        props = excludeMode ? props.Where(p => !propNames.Contains(p.Name)) : props.Where(p => propNames.Contains(p.Name));

        return props.Select(p => Builders<T>.Update.Set(p.Name, p.GetValue(entity)));
    }

    internal static FilterDefinition<T> MergeWithGlobalFilter<T>(bool ignoreGlobalFilters,
                                                                 Dictionary<Type, (object filterDef, bool prepend)>? globalFilters,
                                                                 FilterDefinition<T> filter) where T : IEntity
    {
        //WARNING: this has to do the same thing as DBContext.Pipeline.MergeWithGlobalFilter method
        //         if the following logic changes, update the other method also

        if (ignoreGlobalFilters || !(globalFilters?.Count > 0) || !globalFilters.TryGetValue(typeof(T), out var gFilter))
            return filter;

        return gFilter.filterDef switch
        {
            FilterDefinition<T> definition => gFilter.prepend ? definition & filter : filter & definition,
            BsonDocument bsonDoc => gFilter.prepend ? bsonDoc & filter : filter & bsonDoc,
            string jsonString => gFilter.prepend ? jsonString & filter : filter & jsonString,
            _ => filter
        };
    }
}