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

        internal async Task<Tuple<ObjectId, ObjectId>> SendVessage(ObjectId receicerId, Vessage vessage)
        {
            vessage.Id = ObjectId.GenerateNewId();
            var collection = Client.GetDatabase("Vessage").GetCollection<VessageBox>("VessageBox");
            var update = new UpdateDefinitionBuilder<VessageBox>().Push(vb => vb.Vessages, vessage);
            try
            {
                var result = await collection.FindOneAndUpdateAsync(vb => vb.UserId == receicerId, update);
                if (result == null)
                {
                    var newVb = new VessageBox()
                    {
                        UserId = receicerId,
                        Vessages = new Vessage[] { vessage },
                        LastGetMessageTime = DateTime.UtcNow.AddMinutes(-2),
                        LastGotMessageTime = DateTime.UtcNow.AddMinutes(-2)
                    };
                    await collection.InsertOneAsync(newVb);
                    result = newVb;
                }
                return new Tuple<ObjectId, ObjectId>(result.Id, vessage.Id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal async Task<Tuple<ObjectId,ObjectId>> SendVessageForMobile(string receicerMobile, Vessage vessage)
        {
            vessage.Id = ObjectId.GenerateNewId();
            var collection = Client.GetDatabase("Vessage").GetCollection<VessageBox>("VessageBox");
            var update = new UpdateDefinitionBuilder<VessageBox>().Push(vb => vb.Vessages, vessage);
            try
            {
                var result = await collection.FindOneAndUpdateAsync(vb => vb.ForMobile == receicerMobile, update);
                if (result == null)
                {
                    var newVb = new VessageBox()
                    {
                        ForMobile = receicerMobile,
                        Vessages = new Vessage[] { vessage },
                        LastGetMessageTime = DateTime.UtcNow.AddMinutes(-2),
                        LastGotMessageTime = DateTime.UtcNow.AddMinutes(-2)
                    };
                    await collection.InsertOneAsync(newVb);
                    result = newVb;
                }
                return new Tuple<ObjectId, ObjectId>(result.Id, vessage.Id);
            }
            catch (Exception)
            {
                return null;
            }
            
        }

        internal async Task<bool> CancelSendVessage(string vbId, string senderId, string vessageId)
        {
            var vbOId = new ObjectId(vbId);
            var vessageOId = new ObjectId(vessageId);
            var senderOId = new ObjectId(senderId);
            var collection = Client.GetDatabase("Vessage").GetCollection<VessageBox>("VessageBox");
            var update = Builders<VessageBox>.Update.PullFilter(vb => vb.Vessages, v => v.Id == vessageOId && v.Sender == senderOId);
            var result = await collection.UpdateManyAsync(vb => vb.Id == vbOId, update);
            return result.ModifiedCount > 0;
        }

        internal async Task<bool> FinishSendVessage(string vbId,string senderId, string vessageId, string fileId)
        {
            var collection = Client.GetDatabase("Vessage").GetCollection<BsonDocument>("VessageBox");
            var filter1 = Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(vbId));
            var filter2 = Builders<BsonDocument>.Filter.Eq("Vessages.Sender", new ObjectId(senderId));
            var filter3 = Builders<BsonDocument>.Filter.Eq("Vessages._id", new ObjectId(vessageId));
            var update1 = new UpdateDefinitionBuilder<BsonDocument>().Set("Vessages.$.VideoReady", true);
            var update2 = new UpdateDefinitionBuilder<BsonDocument>().Set("Vessages.$.Video", fileId);
            var update = Builders<BsonDocument>.Update.Combine(update1, update2);
            var result = await collection.UpdateManyAsync(filter1 & filter2 & filter1, update);
            return result.ModifiedCount > 0;
        }

        internal async Task<bool> SetVessageRead(string userId, string vid)
        {
            var collection = Client.GetDatabase("Vessage").GetCollection<BsonDocument>("VessageBox");
            var filter1 = Builders<BsonDocument>.Filter.Eq("UserId", new ObjectId(userId));
            var filter2 = Builders<BsonDocument>.Filter.Eq("Vessages._id", new ObjectId(vid));
            var update = new UpdateDefinitionBuilder<BsonDocument>().Set("Vessages.$.IsRead", true);
            var result = await collection.UpdateManyAsync(filter1 & filter2, update);
            return result.ModifiedCount > 0;
        }

        internal async Task<bool> UpdateGodMessageTime(string userId)
        {
            var userOId = new ObjectId(userId);
            var collection = Client.GetDatabase("Vessage").GetCollection<VessageBox>("VessageBox");
            try
            {
                var vb = await collection.Find(v => v.UserId == userOId).FirstAsync();
                if (vb != null)
                {
                    var updateGotTime = new UpdateDefinitionBuilder<VessageBox>().Set(v => v.LastGotMessageTime, vb.LastGetMessageTime);
                    var result = await collection.UpdateOneAsync(v => v.UserId == userOId, updateGotTime);
                    return result.ModifiedCount > 0;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
            
            
        }

        internal async Task<IEnumerable<Vessage>> GetNotReadMessageOfUser(string userId)
        {
            var userOId = new ObjectId(userId);
            var collection = Client.GetDatabase("Vessage").GetCollection<VessageBox>("VessageBox");
            var vbs = await collection.Find(vb => vb.UserId == userOId).ToListAsync();
            var result = new List<Vessage>();
            foreach (var vb in vbs)
            {
                var vs = from v in vb.Vessages where v.VideoReady && v.SendTime > vb.LastGotMessageTime && v.IsRead == false select v;
                result.AddRange(vs);
            }
            var updateGetTime = new UpdateDefinitionBuilder<VessageBox>().Set(vb => vb.LastGetMessageTime, DateTime.UtcNow);
            await collection.UpdateOneAsync(vb => vb.UserId == userOId, updateGetTime);
            return result;
        }

        internal async Task<VessageBox> BindNewUserReveicedVessages(string userId, string mobile)
        {
            var collection = Client.GetDatabase("Vessage").GetCollection<VessageBox>("VessageBox");
            var update1 = new UpdateDefinitionBuilder<VessageBox>().Set(vb => vb.UserId, new ObjectId(userId));
            var update2 = new UpdateDefinitionBuilder<VessageBox>().Unset(vb => vb.ForMobile);
            var update = Builders<VessageBox>.Update.Combine(update1, update2);
            var result = await collection.FindOneAndUpdateAsync(vb => vb.ForMobile == mobile, update);
            if (result == null)
            {
                result = new VessageBox()
                {
                    UserId = new ObjectId(userId),
                    LastGetMessageTime = DateTime.UtcNow,
                    LastGotMessageTime = DateTime.UtcNow,
                    Vessages = new Vessage[0]
                };
                await collection.InsertOneAsync(result);
            }
            return result;
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
