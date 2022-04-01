﻿using MongoDB.Driver.GeoJsonObjectModel;

namespace MongoDB.Entities;

/// <summary>
/// Represents a 2D geographical coordinate consisting of longitude and latitude
/// </summary>
public class Coordinates2D
{
    [BsonElement("type")]
    public string Type { get; set; }

    [BsonElement("coordinates")]
    public double[] Coordinates { get; set; }

    /// <summary>
    /// Instantiate a new Coordinates2D instance with the supplied longtitude and latitude
    /// </summary>
    public Coordinates2D(double longitude, double latitude)
    {
        Type = "Point";
        Coordinates = new[] { longitude, latitude };
    }

    /// <summary>
    /// Converts a Coordinates2D instance to a GeoJsonPoint of GeoJson2DGeographicCoordinates 
    /// </summary>
    public GeoJsonPoint<GeoJson2DGeographicCoordinates> ToGeoJsonPoint()
    {
        return GeoJson.Point(GeoJson.Geographic(Coordinates[0], Coordinates[1]));
    }

    /// <summary>
    /// Create a GeoJsonPoint of GeoJson2DGeographicCoordinates with supplied longitude and latitude
    /// </summary>
    public static GeoJsonPoint<GeoJson2DGeographicCoordinates> GeoJsonPoint(double longitude, double latitude)
    {
        return GeoJson.Point(GeoJson.Geographic(longitude, latitude));
    }
}

/// <summary>
/// Fluent aggregation pipeline builder for GeoNear
/// </summary>
/// <typeparam name="T">The type of entity</typeparam>
public class GeoNear<T>
{
#pragma warning disable IDE1006
    public Coordinates2D near { get; set; } = null!;
    public string? distanceField { get; set; }
    public bool spherical { get; set; }
    [BsonIgnoreIfNull] public int? limit { get; set; }
    [BsonIgnoreIfNull] public double? maxDistance { get; set; }
    [BsonIgnoreIfNull] public BsonDocument? query { get; set; }
    [BsonIgnoreIfNull] public double? distanceMultiplier { get; set; }
    [BsonIgnoreIfNull] public string? includeLocs { get; set; }
    [BsonIgnoreIfNull] public double? minDistance { get; set; }
    [BsonIgnoreIfNull] public string? key { get; set; }
#pragma warning restore IDE1006

    internal IAggregateFluent<T> ToFluent(DBContext context, AggregateOptions? options = null, string? collectionName = null, IMongoCollection<T>? collection = null)
    {
        var stage = new BsonDocument { { "$geoNear", this.ToBsonDocument() } };

        return context.Session == null
                ? context.Collection(collectionName, collection).Aggregate(options).AppendStage<T>(stage)
                : context.Collection(collectionName, collection).Aggregate(context.Session, options).AppendStage<T>(stage);
    }
}
