using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VessageRESTfulServer.Models;
using MongoDB.Bson;

namespace VessageRESTfulServer.Services
{
    public class VessageService
    {
        protected IMongoClient Client { get; set; }
        public VessageService(IMongoClient Client)
        {
            this.Client = Client;
        }

        internal async Task<Vessage> SendVessage(ObjectId reveicerId, Vessage vessage)
        {
            var collection = Client.GetDatabase("Vessage").GetCollection<VessageBox>("VessageBox");
            var update = new UpdateDefinitionBuilder<VessageBox>().Push(vb => vb.Vessages, vessage);
            var result = await collection.UpdateOneAsync(vb => vb.UserId == reveicerId, update);
            return vessage;
        }

        internal async Task<Vessage> SendVessageForMobile(string reveicerMobile, Vessage vessage)
        {
            var collection = Client.GetDatabase("Vessage").GetCollection<VessageBox>("VessageBox");
            var update = new UpdateDefinitionBuilder<VessageBox>().Push(vb => vb.Vessages, vessage);
            var result = await collection.UpdateOneAsync(vb => vb.ForMobile == reveicerMobile, update);
            return vessage;
        }

        internal async Task<bool> SetVessageRead(string userId, string vid)
        {
            var collection = Client.GetDatabase("Vessage").GetCollection<BsonDocument>("VessageBox");
            var filter1 = Builders<BsonDocument>.Filter.Eq("Vessages.Id", new ObjectId(vid));
            var filter2 = Builders<BsonDocument>.Filter.Eq("UserId", new ObjectId(userId));
            var update = new UpdateDefinitionBuilder<BsonDocument>().Set("IsRead", true);
            var result = await collection.UpdateManyAsync(filter1 & filter2, update);
            return result.ModifiedCount > 0;
        }

        internal async Task UpdateGodMessageTime(string userId)
        {
            var userOId = new ObjectId(userId);
            var collection = Client.GetDatabase("Vessage").GetCollection<VessageBox>("VessageBox");
            var vb = await collection.Find(v => v.UserId == userOId).FirstAsync();
            var updateGotTime = new UpdateDefinitionBuilder<VessageBox>().Set(v => v.LastGotMessageTime, vb.LastGetMessageTime);
            await collection.UpdateOneAsync(v => v.UserId == userOId, updateGotTime);
        }

        internal async Task<IEnumerable<Vessage>> GetNotReadMessageOfUser(string userId)
        {
            var userOId = new ObjectId(userId);
            var collection = Client.GetDatabase("Vessage").GetCollection<VessageBox>("VessageBox");
            var vbs = await collection.Find(vb => vb.UserId == userOId).ToListAsync();
            var result = new List<Vessage>();
            foreach (var vb in vbs)
            {
                var vs = from v in vb.Vessages where v.SendTime > vb.LastGotMessageTime & v.IsRead == false select v;
                result.AddRange(vs);
            }
            var updateGetTime = new UpdateDefinitionBuilder<VessageBox>().Set(vb => vb.LastGetMessageTime, DateTime.UtcNow);
            await collection.UpdateOneAsync(vb => vb.UserId == userOId, updateGetTime);
            return result;
        }

        internal async Task<bool> BindNewUserReveicedVessages(string userId, string mobile)
        {
            var collection = Client.GetDatabase("Vessage").GetCollection<VessageBox>("VessageBox");
            var update = new UpdateDefinitionBuilder<VessageBox>().Set(vb => vb.UserId, new ObjectId(userId));
            var result = await collection.UpdateOneAsync(vb => vb.ForMobile == mobile && (vb.UserId == null || ObjectId.Empty == vb.UserId), update);
            return result.ModifiedCount > 0;
        }
    }

    public static class GetVessageServiceExtension
    {
        public static VessageService GetVessageService(this IServiceProvider provider)
        {
            return provider.GetService<VessageService>();
        }
    }
}
