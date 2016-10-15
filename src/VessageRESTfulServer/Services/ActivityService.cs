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
        public string[] MiniBadgeActivity { get; set; }
        public string[] BadgeValueActivity { get; set; }
    }

    public class ActivityService
    {
        protected IMongoClient Client { get; set; }
        public IMongoDatabase ActivityBadgeDataDb { get { return Client.GetDatabase("ActivityBadgeData"); } }
        public ActivityService(IMongoClient Client)
        {
            this.Client = Client;
        }

        public async void AddActivityBadge(string activityId, ObjectId userId, int addiction)
        {
            var collection = ActivityBadgeDataDb.GetCollection<ActivityBadgeData>("ActivityBadgeData");
            try
            {

                var update = new UpdateDefinitionBuilder<ActivityBadgeData>().Push(d => d.BadgeValueActivity, string.Format("{0}:{1}", activityId, addiction));
                var res = await collection.UpdateOneAsync(a => a.UserId == userId, update);
                if (res.MatchedCount == 0)
                {
                    var data = new ActivityBadgeData
                    {
                        BadgeValueActivity = new string[] { string.Format("{0}:{1}", activityId, addiction) },
                        MiniBadgeActivity = new string[0],
                        UserId = userId
                    };
                    await collection.InsertOneAsync(data);
                }
            }
            catch (Exception)
            {
                
            }
            
        }

        public async void SetActivityMiniBadge(string activityId, ObjectId userId)
        {
            var collection = ActivityBadgeDataDb.GetCollection<ActivityBadgeData>("ActivityBadgeData");
            try
            {
                var update = new UpdateDefinitionBuilder<ActivityBadgeData>().Push(d => d.MiniBadgeActivity, activityId);
                var res = await collection.UpdateOneAsync(a => a.UserId == userId, update);
                if (res.MatchedCount == 0)
                {
                    var data = new ActivityBadgeData
                    {
                        BadgeValueActivity = new string[0],
                        MiniBadgeActivity = new string[] { activityId },
                        UserId = userId
                    };
                    await collection.InsertOneAsync(data);
                }
            }
            catch (Exception)
            {
                
            }
        }

        public async Task<ActivityBadgeData> GetActivityBoardData(string userId)
        {
            var userOId = new ObjectId(userId);
            var collection = ActivityBadgeDataDb.GetCollection<ActivityBadgeData>("ActivityBadgeData");
            var update1 = new UpdateDefinitionBuilder<ActivityBadgeData>().Set(d => d.BadgeValueActivity, new string[0]);
            var update2 = new UpdateDefinitionBuilder<ActivityBadgeData>().Set(d => d.MiniBadgeActivity, new string[0]);
            var update = new UpdateDefinitionBuilder<ActivityBadgeData>().Combine(update1, update2);
            return await collection.FindOneAndUpdateAsync(a => a.UserId == userOId, update);
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
