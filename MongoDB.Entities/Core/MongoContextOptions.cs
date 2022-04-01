﻿using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;

#nullable enable

namespace MongoDB.Entities
{
    public class MongoContextOptions
    {
        public MongoContextOptions(ModifiedBy? modifiedBy = null)
        {
            ModifiedBy = modifiedBy;
        }

        /// <summary>
        /// The value of this property will be automatically set on entities when saving/updating if the entity has a ModifiedBy property
        /// </summary>
        public ModifiedBy? ModifiedBy { get; set; }
    }
}
