﻿using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using Sylph.Models;

namespace Sylph.Services
{
    public class HurricaneService
    {
        private readonly IMongoCollection<Hurricane> _collection;
        public HurricaneService(HurricaneDatabaseSettings.IHurricaneDatabaseSettings settings)
        {
            MongoClient client = new MongoClient(settings.ConnectionString);
            IMongoDatabase database = client.GetDatabase(settings.DatabaseName);
            _collection = database.GetCollection<Hurricane>(settings.HurricaneCollectionName);
        }

        public List<Hurricane> GetAllHurricanes()
        {
            return _collection.Find(hurricane => true).ToList();
        }

        public List<Hurricane> GetHurricane(long startdate, long enddate, int maxspeed, short active, string name, string sortorder)
        {
            
            var filter = Builders<Hurricane>.Filter;
            
            var startDateFilter = filter.Gte("firstActive", startdate);
            var endDateFilter = filter.Lte("firstActive", enddate);
            var maxSpeedFilter = filter.Lte("maxSpeed", maxspeed);
            var activeFilter = filter.Eq("active", active == 1?true:false);
            var nameFilter = filter.Regex("name", new BsonRegularExpression(name.ToUpperInvariant()));
            var sortDefinition = Builders<Hurricane>.Sort.Descending(hurricane => hurricane.firstActive);
            if (sortorder.Equals("asc"))
            {
                sortDefinition = Builders<Hurricane>.Sort.Ascending(hurricane => hurricane.firstActive);
            }

            if (active != 0 && active != 1)
            { //not filtering activity, show both active & inactive
                return _collection.Find(startDateFilter & endDateFilter & maxSpeedFilter & nameFilter)
                    .Sort(sortDefinition).ToList();
            }
            return _collection.Find(startDateFilter & endDateFilter & maxSpeedFilter & activeFilter & nameFilter)
                .Sort(sortDefinition).ToList();
        }
    }
}