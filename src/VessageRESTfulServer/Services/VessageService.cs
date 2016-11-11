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
        public IMongoDatabase VessageDb { get { return Client.GetDatabase("Vessage"); } }

        public VessageService(IMongoClient Client)
        {
            this.Client = Client;
        }

        public async Task<ObjectId> SendVessagesToUser(ObjectId userId, IEnumerable<Vessage> vessages)
        {
            foreach (var vsg in vessages)
            {
                if (vsg.Id == ObjectId.Empty)
                {
                    vsg.Id = ObjectId.GenerateNewId();
                }
            }
            var collection = VessageDb.GetCollection<VessageBox>("VessageBox");
            var update = new UpdateDefinitionBuilder<VessageBox>().PushEach(vb => vb.Vessages, vessages);
            try
            {
                var result = await collection.FindOneAndUpdateAsync(vb => vb.UserId == userId, update);
                if (result == null)
                {
                    var newVb = new VessageBox()
                    {
                        UserId = userId,
                        Vessages = vessages.ToArray(),
                        LastGetMessageTime = DateTime.UtcNow.AddMinutes(-2),
                        LastGotMessageTime = DateTime.UtcNow.AddMinutes(-2),
                        IsGroup = false
                    };
                    await collection.InsertOneAsync(newVb);
                    result = newVb;
                }
                return result.Id;
            }
            catch (Exception)
            {
                return ObjectId.Empty;
            }
        }

        public async Task<Tuple<ObjectId, ObjectId>> SendVessage(ObjectId receicerId, Vessage vessage,bool isGroup)
        {
            vessage.Id = ObjectId.GenerateNewId();
            var collection = VessageDb.GetCollection<VessageBox>("VessageBox");
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
                        LastGotMessageTime = DateTime.UtcNow.AddMinutes(-2),
                        IsGroup = isGroup
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

        /// <summary>
        /// 已过时
        /// </summary>
        /// <param name="receicerMobile"></param>
        /// <param name="vessage"></param>
        /// <returns></returns>
        private async Task<Tuple<ObjectId,ObjectId>> SendVessageForMobile(string receicerMobile, Vessage vessage)
        {
            vessage.Id = ObjectId.GenerateNewId();
            var collection = VessageDb.GetCollection<VessageBox>("VessageBox");
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
                        LastGotMessageTime = DateTime.UtcNow.AddMinutes(-2),
                        IsGroup = false
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

        public async Task SendGroupVessageToChatters(ObjectId groupId, IEnumerable<ObjectId> chatters, ObjectId vessageId)
        {
            var collection = VessageDb.GetCollection<VessageBox>("VessageBox");
            var vb = await collection.Find(f => f.UserId == groupId && f.IsGroup).FirstAsync();
            var vsg = vb.Vessages.First(f => f.Id == vessageId);
            foreach (var chatter in chatters)
            {
                await SendVessage(chatter, vsg, false);
            }
            var update = new UpdateDefinitionBuilder<VessageBox>().PullFilter(f => f.Vessages, d => d.Id == vessageId);
            await collection.UpdateOneAsync(f => f.UserId == groupId && f.IsGroup, update);
        }

        public async Task<bool> CancelSendVessage(string vbId, string senderId, string vessageId)
        {
            var vbOId = new ObjectId(vbId);
            var vessageOId = new ObjectId(vessageId);
            var senderOId = new ObjectId(senderId);
            var collection = VessageDb.GetCollection<VessageBox>("VessageBox");
            var update = Builders<VessageBox>.Update.PullFilter(vb => vb.Vessages, v => v.Id == vessageOId && v.Sender == senderOId);
            var result = await collection.UpdateManyAsync(vb => vb.Id == vbOId, update);
            return result.ModifiedCount > 0;
        }

        public class FinishSendVessageResult
        {
            public ObjectId ReceiverId { get; set; }
            public string ReceiverMobile { get; set; }
            public bool ReceiverIsGroup { get; set; }
        }

        public async Task<FinishSendVessageResult> FinishSendVessage(ObjectId vbId, ObjectId senderId, ObjectId vessageId, string fileId)
        {
            var collection = VessageDb.GetCollection<BsonDocument>("VessageBox");
            var filter1 = Builders<BsonDocument>.Filter.Eq("_id", vbId);
            var filter2 = Builders<BsonDocument>.Filter.Eq("Vessages.Sender", senderId);
            var filter2_1 = Builders<BsonDocument>.Filter.Eq("IsGroup", true);
            var filter3 = Builders<BsonDocument>.Filter.Eq("Vessages._id", vessageId);
            var filter = filter1 & (filter2_1 | filter2) & filter3;
            var update1 = new UpdateDefinitionBuilder<BsonDocument>().Set("Vessages.$.Ready", true);
            var update2 = new UpdateDefinitionBuilder<BsonDocument>().Set("Vessages.$.FileId", fileId);
            var update = Builders<BsonDocument>.Update.Combine(update1, update2);
            var result = await collection.FindOneAndUpdateAsync(filter, update);
            if (result != null)
            {
                BsonValue outUserId;
                BsonValue outMobile;
                BsonValue outIsGroup;
                result.TryGetValue("UserId", out outUserId);
                result.TryGetValue("ForMobile", out outMobile);
                result.TryGetValue("IsGroup", out outIsGroup);
                
                return new FinishSendVessageResult
                {
                    ReceiverId = outUserId != null && outUserId.IsObjectId ? outUserId.AsObjectId : ObjectId.Empty,
                    ReceiverIsGroup = outIsGroup != null && outIsGroup.IsBoolean ? outIsGroup.AsBoolean : false,
                    ReceiverMobile = outMobile != null && outMobile.IsString ? outMobile.AsString : null
                };
            }
            return null;
        }

        public async Task<bool> SetVessageRead(string userId, string vid)
        {
            var collection = VessageDb.GetCollection<BsonDocument>("VessageBox");
            var filter1 = Builders<BsonDocument>.Filter.Eq("UserId", new ObjectId(userId));
            var filter2 = Builders<BsonDocument>.Filter.Eq("Vessages._id", new ObjectId(vid));
            var update = new UpdateDefinitionBuilder<BsonDocument>().Set("Vessages.$.IsRead", true);
            var result = await collection.UpdateManyAsync(filter1 & filter2, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateGotMessageTime(string userId)
        {
            var userOId = new ObjectId(userId);
            var collection = VessageDb.GetCollection<VessageBox>("VessageBox");
            try
            {
                var vb = await collection.Find(v => v.UserId == userOId).FirstAsync();
                if (vb != null)
                {
                    var updateGotTime = new UpdateDefinitionBuilder<VessageBox>().Set(v => v.LastGotMessageTime, vb.LastGetMessageTime);
                    var updateVessages = new UpdateDefinitionBuilder<VessageBox>().PullFilter(v => v.Vessages, st => st.Ready && st.SendTime < vb.LastGetMessageTime);
                    var update = new UpdateDefinitionBuilder<VessageBox>().Combine(updateGotTime, updateVessages);
                    var result = await collection.UpdateOneAsync(v => v.UserId == userOId, update);
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
            var collection = VessageDb.GetCollection<VessageBox>("VessageBox");
            var vbs = await collection.Find(vb => vb.UserId == userOId).ToListAsync();
            var result = new List<Vessage>();
            foreach (var vb in vbs)
            {
                var vs = from v in vb.Vessages where v.Ready && v.SendTime > vb.LastGotMessageTime && v.IsRead == false select v;
                result.AddRange(vs);
            }
            var updateGetTime = new UpdateDefinitionBuilder<VessageBox>().Set(vb => vb.LastGetMessageTime, DateTime.UtcNow);
            await collection.UpdateOneAsync(vb => vb.UserId == userOId, updateGetTime);
            return result;
        }

        /// <summary>
        /// 已过时
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="mobile"></param>
        /// <returns></returns>
        private async Task<VessageBox> BindNewUserReveicedVessages(string userId, string mobile)
        {
            var collection = VessageDb.GetCollection<VessageBox>("VessageBox");
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
