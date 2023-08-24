﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MongoDB.Entities.Tests;

[TestClass]
public class GeoNearTest
{
    public static IEnumerable<object[]> PlaceData
    {
        get
        {
            var guid = Guid.NewGuid().ToString();
            return new[]
            {
                    new object[]
                    {
                            guid,
                            new Place[]{
                                new PlaceEntity{ Name = "Paris " + guid, Location = new Coordinates2D(48.8539241, 2.2913515) },
                                new PlaceEntity{ Name = "Versailles " + guid, Location = new Coordinates2D(48.796964, 2.137456) },
                                new PlaceEntity{ Name = "Poissy " + guid, Location = new Coordinates2D(48.928860, 2.046889) }
                            }
                    },
                    new object[]
                    {
                            guid,
                            new Place[]{
                                    new PlaceGuid{ Name = "Paris " + guid, Location = new Coordinates2D(48.8539241, 2.2913515) },
                                    new PlaceGuid{ Name = "Versailles " + guid, Location = new Coordinates2D(48.796964, 2.137456) },
                                    new PlaceGuid{ Name = "Poissy " + guid, Location = new Coordinates2D(48.928860, 2.046889) }
                            }
                    },
                    new object[]
                    {
                            guid,
                            new Place[]{
                                    new PlaceInt64{ Name = "Paris " + guid, Location = new Coordinates2D(48.8539241, 2.2913515) },
                                    new PlaceInt64{ Name = "Versailles " + guid, Location = new Coordinates2D(48.796964, 2.137456) },
                                    new PlaceInt64{ Name = "Poissy " + guid, Location = new Coordinates2D(48.928860, 2.046889) }
                            }
                    },
                    new object[]
                    {
                            guid,
                            new Place[]{
                                    new PlaceObjectId{ Name = "Paris " + guid, Location = new Coordinates2D(48.8539241, 2.2913515) },
                                    new PlaceObjectId{ Name = "Versailles " + guid, Location = new Coordinates2D(48.796964, 2.137456) },
                                    new PlaceObjectId{ Name = "Poissy " + guid, Location = new Coordinates2D(48.928860, 2.046889) }
                            }
                    },
            };
        }
    }
    
    [TestMethod]
    [DynamicData(nameof(PlaceData))]
    public async Task find_match_geo_method(string guid, Place[] places)
    {
        await DB.Index<Place>()
          .Key(x => x.Location, KeyType.Geo2DSphere)
          .Option(x => x.Background = false)
          .CreateAsync();

        await places.SaveAsync();

        var res = (await DB.Find<Place>()
                    .Match(p => p.Location, new Coordinates2D(48.857908, 2.295243), 20000) //20km from eiffel tower
                    .Sort(p => p.ModifiedOn, Order.Descending)
                    .Limit(20)
                    .ExecuteAsync())
                    .Where(c => c.Name == "Paris " + guid)
                    .ToList();

        Assert.AreEqual(1, res.Count);
    }

    [TestMethod]
    [DynamicData(nameof(PlaceData))]
    public async Task geo_near_fluent_interface(string guid, Place[] places)
    {
        await DB.Index<Place>()
            .Key(x => x.Location, KeyType.Geo2DSphere)
            .Option(x => x.Background = false)
            .CreateAsync();

        await places.SaveAsync();

        var qry = DB.FluentGeoNear<Place>(
                     NearCoordinates: new Coordinates2D(48.857908, 2.295243), //eiffel tower
                     DistanceField: x => x.DistanceKM,
                     MaxDistance: 20000);

        var cnt = await qry.Match(c => c.Name.Contains(guid)).ToListAsync();
        Assert.AreEqual(2, cnt.Count);

        var res = await qry.Match(c => c.Name == "Paris " + guid).ToListAsync();
        Assert.AreEqual(1, res.Count);
    }

    [TestMethod]
    [DynamicData(nameof(PlaceData))]
    public async Task geo_near_transaction_returns_correct_results(string guid, Place[] places)
    {
        await DB.Index<Place>()
            .Key(x => x.Location, KeyType.Geo2DSphere)
            .Option(x => x.Background = false)
            .CreateAsync();
        
        using var TN = new Transaction();

        await places.SaveAsync();

        var qry = TN.GeoNear<Place>(
                     NearCoordinates: new Coordinates2D(48.857908, 2.295243), //eiffel tower
                     DistanceField: x => x.DistanceKM,
                     MaxDistance: 20000);

        var cnt = await qry.Match(c => c.Name.Contains(guid)).ToListAsync();
        Assert.AreEqual(2, cnt.Count);

        var res = await qry.Match(c => c.Name == "Paris " + guid).ToListAsync();
        Assert.AreEqual(1, res.Count);
    }
}
