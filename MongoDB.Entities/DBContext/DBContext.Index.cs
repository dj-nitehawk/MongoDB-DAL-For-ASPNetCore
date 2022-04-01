﻿using MongoDB.Driver;

namespace MongoDB.Entities
{
    public partial class DBContext
    {
        /// <summary>
        /// Represents an index for a given IEntity
        /// <para>TIP: Define the keys first with .Key() method and finally call the .Create() method.</para>
        /// </summary>
        /// <typeparam name="T">Any class</typeparam>
        public Index<T> Index<T>(string? collectionName = null, IMongoCollection<T>? collection = null)
        {
            return new Index<T>(this, Collection(collectionName, collection));
        }
         
    }
}
