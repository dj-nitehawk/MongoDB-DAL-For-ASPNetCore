﻿using System;
using Medo;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDB.Entities.Tests;

[Collection("GenreUuid")]
public class GenreUuid : Genre
{
  [BsonId]
  public string? ID { get; set; }
  public override object GenerateNewID()
    => Uuid7.NewUuid7().ToString();

  [InverseSide]
  public Many<BookUuid, GenreUuid> Books { get; set; }

  public GenreUuid() => this.InitManyToMany(() => Books, b => b.Genres);

}