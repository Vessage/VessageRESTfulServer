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

        public ActivityBadgeData Copy()
        {
            return new ActivityBadgeData
            {
                Id = this.Id,
                UserId = this.UserId,
                Activities = (from a in this.Activities select a.Copy()).ToArray()
            };
        }

        public void ClearBadges()
        {
            foreach (var item in Activities)
            {

                item.Badge = 0;
                item.MiniBadge = false;
                item.Message = null;
            }
        }
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

        public string Message { get; set; }

        public BadgeData Copy()
        {
            return new BadgeData
            {
                AcId = this.AcId,
                Badge = this.Badge,
                MiniBadge = this.MiniBadge,
                Message = this.Message
            };
        }
    }

    public class ActivityService
    {
        protected IMongoClient Client { get; set; }
        public IMongoDatabase ActivityBadgeDataDb { get { return Client.GetDatabase("ActivityBadgeData"); } }
        public ActivityService(IMongoClient Client)
        {
            this.Client = Client;
        }

        public async Task<bool> AddActivityBadge(string activityId, ObjectId userId, int addiction, string message = null)
        {
            try
            {
                var collection = ActivityBadgeDataDb.GetCollection<BsonDocument>("ActivityBadgeData");
                var filterUserId = new FilterDefinitionBuilder<BsonDocument>().Eq("UserId", userId);
                var filterAc = new FilterDefinitionBuilder<BsonDocument>().Eq("Activities.AcId", activityId);
                var incUpdate = new UpdateDefinitionBuilder<BsonDocument>().Inc("Activities.$.Badge", addiction);
                if (string.IsNullOrEmpty(message) == false)
                {
                    incUpdate.Set("Activities.$.Message", message);
                }
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
                        Message = message
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

        public async Task CreateActivityBadgeData(string activityId, ObjectId userId)
        {
            await SetActivityMiniBadge(activityId, userId, true);
        }

        public async Task<bool> SetActivityMiniBadge(string activityId, ObjectId userId, bool miniBadge = true, string message = null)
        {
            try
            {
                var collection = ActivityBadgeDataDb.GetCollection<BsonDocument>("ActivityBadgeData");
                var filterUserId = new FilterDefinitionBuilder<BsonDocument>().Eq("UserId", userId);
                var filterAc = new FilterDefinitionBuilder<BsonDocument>().Eq("Activities.AcId", activityId);
                var update = new UpdateDefinitionBuilder<BsonDocument>().Set("Activities.$.MiniBadge", miniBadge);
                if (string.IsNullOrEmpty(message) == false)
                {
                    update.Set("Activities.$.Message", message);
                }
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
                        MiniBadge = miniBadge,
                        Message = message
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

            try
            {
                var data = await collection.Find(f => f.UserId == userId).FirstAsync();
                if (data != null && data.Activities != null && data.Activities.Count() > 0)
                {
                    var copy = data.Copy();
                    copy.ClearBadges();
                    var update = new UpdateDefinitionBuilder<ActivityBadgeData>().Set("Activities", copy.Activities);
                    await collection.UpdateOneAsync(f => f.UserId == userId, update);
                }
                return data;
            }
            catch (System.Exception e)
            {
                throw e;
            }

        }

        public async Task SetActivityMiniBadgeOfUserIds(string activityId, IEnumerable<ObjectId> followers, bool miniBadge = true, string message = null)
        {
            if (followers.Count() > 0)
            {
                var collection = ActivityBadgeDataDb.GetCollection<ActivityBadgeData>("ActivityBadgeData");
                var filter = new FilterDefinitionBuilder<ActivityBadgeData>().In(f => f.UserId, followers);
                var filter2 = new FilterDefinitionBuilder<ActivityBadgeData>().Eq("Activities.AcId", activityId);
                var update = new UpdateDefinitionBuilder<ActivityBadgeData>().Set("Activities.$.MiniBadge", miniBadge);
                if (string.IsNullOrEmpty(message) == false)
                {
                    update.Set("Activities.$.Message", message);
                }
                await collection.UpdateManyAsync(filter & filter2, update);
            }
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
