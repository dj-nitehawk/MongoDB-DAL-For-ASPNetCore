﻿using MongoDB.Bson;
using System;

namespace MongoDB.Entities;

public static partial class DB
{
    /// <summary>
    /// Returns a DataStreamer object to enable uploading/downloading file data directly by supplying the ID of the file entity
    /// </summary>
    /// <typeparam name="T">The file entity type</typeparam>
    /// <param name="ID">The ID of the file entity</param>
    public static DataStreamer File<T>(string? ID) where T : FileEntity, new()
    {
        return !ObjectId.TryParse(ID, out _)
            ? throw new ArgumentException("The ID passed in is not of the correct format!")
            : new DataStreamer(new T() { ID = ID, UploadSuccessful = true });
    }
}
