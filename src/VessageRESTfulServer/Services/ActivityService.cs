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

        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }

        public string AcId { get; set; }
        public int Badge { get; set; }
        public bool MiniBadge { get; set; }

        public string Message { get; set; }

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
                var collection = ActivityBadgeDataDb.GetCollection<ActivityBadgeData>("Badges");
                var update = new UpdateDefinitionBuilder<ActivityBadgeData>().Inc("Badge", addiction).Set("UserId", userId).Set("AcId", activityId);
                if (string.IsNullOrEmpty(message) == false)
                {
                    update = update.Set("Message", message);
                }
                var option = new UpdateOptions
                {
                    IsUpsert = true
                };
                var res = await collection.UpdateOneAsync(f => f.UserId == userId && f.AcId == activityId, update, option);
                return res.ModifiedCount > 0;
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
                var collection = ActivityBadgeDataDb.GetCollection<ActivityBadgeData>("Badges");
                var update = new UpdateDefinitionBuilder<ActivityBadgeData>().Set("MiniBadge", miniBadge).Set("UserId", userId).Set("AcId", activityId);
                if (string.IsNullOrEmpty(message) == false)
                {
                    update = update.Set("Message", message);
                }
                var option = new UpdateOptions
                {
                    IsUpsert = true
                };
                var res = await collection.UpdateOneAsync(f => f.UserId == userId && f.AcId == activityId, update, option);
                return res.ModifiedCount > 0;
            }
            catch (Exception)
            {
            }
            return false;
        }

        public async Task<IEnumerable<ActivityBadgeData>> GetActivityBoardData(ObjectId userId)
        {
            var collection = ActivityBadgeDataDb.GetCollection<ActivityBadgeData>("Badges");

            try
            {
                var data = await collection.Find(f => f.UserId == userId).ToListAsync();
                var update = new UpdateDefinitionBuilder<ActivityBadgeData>().Set("Badge", 0).Set("MiniBadge", false).Set(f => f.Message, null);
                await collection.UpdateManyAsync(f => f.UserId == userId, update);
                return data;
            }
            catch (System.Exception e)
            {
                throw e;
            }

        }

        public async Task SetActivityMiniBadgeOfUserIds(string activityId, IEnumerable<ObjectId> userIds, bool miniBadge = true, string message = null)
        {
            if (userIds.Count() > 0)
            {
                var collection = ActivityBadgeDataDb.GetCollection<ActivityBadgeData>("Badges");

                var filter = new FilterDefinitionBuilder<ActivityBadgeData>().In(f => f.UserId, userIds);
                var filter2 = new FilterDefinitionBuilder<ActivityBadgeData>().Eq("AcId", activityId);

                var update = new UpdateDefinitionBuilder<ActivityBadgeData>().Set("MiniBadge", miniBadge);
                if (string.IsNullOrEmpty(message) == false)
                {
                    update = update.Set("Message", message);
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
