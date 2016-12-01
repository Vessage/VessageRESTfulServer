using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using MongoDB.Bson;

namespace VessageRESTfulServer.Services
{
    public class ActivityBadgeData
    {
        public ActivityBadgeData()
        {
            Activities = new BadgeData[0];
        }

        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }

        public BadgeData[] Activities;
    }

    public class BadgeData
    {
        public BadgeData()
        {
            Badge = 0;
            MiniBadge = false;
        }

        public string AcId { get; set; }
        public int Badge { get; set; }
        public bool MiniBadge { get; set; }
    }

    public class ActivityService
    {
        protected IMongoClient Client { get; set; }
        public IMongoDatabase ActivityBadgeDataDb { get { return Client.GetDatabase("ActivityBadgeData"); } }
        public ActivityService(IMongoClient Client)
        {
            this.Client = Client;
        }

        public async Task<bool> AddActivityBadge(string activityId, ObjectId userId, int addiction)
        {
            try
            {
                var collection = ActivityBadgeDataDb.GetCollection<BsonDocument>("ActivityBadgeData");
                var filterUserId = new FilterDefinitionBuilder<BsonDocument>().Eq("UserId", userId);
                var filterAc = new FilterDefinitionBuilder<BsonDocument>().Eq("Activities.AcId", activityId);
                var incUpdate = new UpdateDefinitionBuilder<BsonDocument>().Inc("Activities.$.Badge", addiction);
                var res = await collection.UpdateOneAsync(filterUserId & filterAc, incUpdate);
                if (res.ModifiedCount == 0)
                {
                    var addToSet = new UpdateDefinitionBuilder<BsonDocument>()
                    .Set("UserId", userId)
                    .AddToSet("Activities", new BadgeData
                    {
                        AcId = activityId,
                        Badge = addiction,
                        MiniBadge = false,
                    });
                    var option = new UpdateOptions
                    {
                        IsUpsert = true
                    };
                    res = await collection.UpdateOneAsync(filterUserId, addToSet, option);
                }
                return res.ModifiedCount > 0 || res.UpsertedId != null;
            }
            catch (Exception)
            {
            }
            return false;
        }

        public async Task<bool> SetActivityMiniBadge(string activityId, ObjectId userId)
        {
            try
            {
                var collection = ActivityBadgeDataDb.GetCollection<BsonDocument>("ActivityBadgeData");
                var filterUserId = new FilterDefinitionBuilder<BsonDocument>().Eq("UserId", userId);
                var filterAc = new FilterDefinitionBuilder<BsonDocument>().Eq("Activities.AcId", activityId);
                var update = new UpdateDefinitionBuilder<BsonDocument>().Set("Activities.$.MiniBadge", true);
                var res = await collection.UpdateOneAsync(filterUserId & filterAc, update);
                if (res.ModifiedCount == 0)
                {
                    var addToSet = new UpdateDefinitionBuilder<BsonDocument>()
                    .Set("AcId", activityId)
                    .Set("UserId", userId)
                    .AddToSet("Activities", new BadgeData
                    {
                        AcId = activityId,
                        Badge = 0,
                        MiniBadge = true,
                    });
                    var option = new UpdateOptions
                    {
                        IsUpsert = true
                    };
                    res = await collection.UpdateOneAsync(filterUserId, addToSet, option);
                }
                return res.ModifiedCount > 0 || res.UpsertedId != null;
            }
            catch (Exception)
            {
            }
            return false;
        }

        public async Task<ActivityBadgeData> GetActivityBoardData(ObjectId userId)
        {
            var collection = ActivityBadgeDataDb.GetCollection<ActivityBadgeData>("ActivityBadgeData");

            var filter = new FilterDefinitionBuilder<ActivityBadgeData>().Eq("UserId", userId);
            var filter2 = new FilterDefinitionBuilder<ActivityBadgeData>().SizeGt("Activities", 0);

            var update = new UpdateDefinitionBuilder<ActivityBadgeData>()
            .Set("Activities", new BadgeData[0]);
            
            var option = new FindOneAndUpdateOptions<ActivityBadgeData>
            {
                ReturnDocument = ReturnDocument.Before
            };
            return await collection.FindOneAndUpdateAsync(filter & filter2, update, option);
        }
    }

    public static class ActivityServiceExtensions
    {
        public static ActivityService GetActivityService(this IServiceProvider provider)
        {
            return provider.GetService<ActivityService>();
        }
    }
}
