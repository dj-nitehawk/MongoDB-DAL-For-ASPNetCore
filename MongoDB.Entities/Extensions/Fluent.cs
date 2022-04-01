﻿using MongoDB.Driver;
using System;

namespace MongoDB.Entities
{
    public static partial class Extensions
    {
        /// <summary>
        /// An IAggregateFluent collection of sibling Entities.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="_"></param>
        /// <param name="options">The options for the aggregation. This is not required.</param>
        /// <param name="ignoreGlobalFilters"></param>
        /// <param name="collectionName"></param>
        /// <param name="collection"></param>
        public static IAggregateFluent<T> Fluent<T>(this T _, AggregateOptions? options = null, bool ignoreGlobalFilters = false, string? collectionName = null, IMongoCollection<T>? collection = null) where T : IEntity
        {
            return DB.Context.Fluent(options, ignoreGlobalFilters: ignoreGlobalFilters, collection: collection, collectionName: collectionName);
        }

        /// <summary>
        /// Adds a distinct aggregation stage to a fluent pipeline.
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        public static IAggregateFluent<T> Distinct<T>(this IAggregateFluent<T> aggregate) where T : IEntity
        {
            PipelineStageDefinition<T, T> groupStage = @"
                                                        {
                                                            $group: {
                                                                _id: '$_id',
                                                                doc: {
                                                                    $first: '$$ROOT'
                                                                }
                                                            }
                                                        }";

            PipelineStageDefinition<T, T> rootStage = @"
                                                        {
                                                            $replaceRoot: {
                                                                newRoot: '$doc'
                                                            }
                                                        }";

            return aggregate.AppendStage(groupStage).AppendStage(rootStage);
        }

        /// <summary>
        /// Appends a match stage to the pipeline with a filter expression
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        /// <param name="aggregate"></param>
        /// <param name="filter">f => f.Eq(x => x.Prop, Value) &amp; f.Gt(x => x.Prop, Value)</param>
        public static IAggregateFluent<T> Match<T>(this IAggregateFluent<T> aggregate, Func<FilterDefinitionBuilder<T>, FilterDefinition<T>> filter) where T : IEntity
        {
            return aggregate.Match(filter(Builders<T>.Filter));
        }

        /// <summary>
        /// Appends a match stage to the pipeline with an aggregation expression (i.e. $expr)
        /// </summary>
        /// <typeparam name="T">Any class that implements IEntity</typeparam>
        /// <param name="aggregate"></param>
        /// <param name="expression">{ $gt: ['$Property1', '$Property2'] }</param>
        public static IAggregateFluent<T> MatchExpression<T>(this IAggregateFluent<T> aggregate, string expression) where T : IEntity
        {
            PipelineStageDefinition<T, T> stage = "{$match:{$expr:" + expression + "}}";

            return aggregate.AppendStage(stage);
        }
    }
}
