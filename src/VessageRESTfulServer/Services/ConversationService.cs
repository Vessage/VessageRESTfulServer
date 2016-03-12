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
    public class ConversationService
    {
        protected IMongoClient Client { get; set; }
        public ConversationService(IMongoClient Client)
        {
            this.Client = Client;
        }

        internal async Task<IEnumerable<Conversation>> GetUserConversations(string userId)
        {
            var userOId = new ObjectId(userId);
            var lists = await Client.GetDatabase("Vessage").GetCollection<ConversationList>("ConversationList").Find(cl => cl.UserId == userOId).ToListAsync();
            var result = new List<Conversation>();
            foreach (var list in lists)
            {
                result.AddRange(list.Conversations);
            }
            return result;
        }

        internal async Task<Conversation> AddConversation(string userId,Conversation conversation)
        {
            var userOId = new ObjectId(userId);
            conversation.UserId = userOId;
            var collection = Client.GetDatabase("Vessage").GetCollection<ConversationList>("ConversationList");
            var update = new UpdateDefinitionBuilder<ConversationList>().Push(c => c.Conversations, conversation);
            var res = await collection.FindOneAndUpdateAsync(c => c.UserId == userOId, update);
            return conversation;
            
        }

        internal async Task<bool> ChangeConversationNoteName(string userId, string conversationId, string noteName)
        {
            var cOId = new ObjectId(conversationId);
            var userOId = new ObjectId(userId);
            var collection = Client.GetDatabase("Vessage").GetCollection<BsonDocument>("ConversationList");
            
            var filter = Builders<BsonDocument>.Filter.Eq("UserId",userOId) & Builders<BsonDocument>.Filter.Eq("Conversations.Id", cOId);
            var update = Builders<BsonDocument>.Update.Set("Conversations.$.NoteName", noteName);
            var result = await collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        internal async Task<bool> RemoveConversation(string userId, string conversationId)
        {
            var userOId = new ObjectId(userId);
            var cOId = new ObjectId(conversationId);
            var collection = Client.GetDatabase("Vessage").GetCollection<ConversationList>("ConversationList");
            var update = new UpdateDefinitionBuilder<ConversationList>().PullFilter(cl => cl.Conversations, c => c.Id == cOId);
            var res = await collection.UpdateOneAsync(c => c.UserId == userOId, update);
            return res.ModifiedCount > 0;
        }

        internal async Task<Conversation> GetConversationOfUser(string userId, string conversationId)
        {
            var uOId = new ObjectId(userId);
            var cOId = new ObjectId(conversationId);
            var collection = Client.GetDatabase("Vessage").GetCollection<ConversationList>("ConversationList");
            var cList = await collection.Find(cl => cl.UserId == uOId).FirstAsync();
            try
            {
                return cList.Conversations.First(c => c.Id == cOId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal async Task<Conversation> GetConversationOfReceiverId(ObjectId chattingUserId, string userId)
        {
            var uOId = new ObjectId(userId);
            var collection = Client.GetDatabase("Vessage").GetCollection<ConversationList>("ConversationList");
            var cList = await collection.Find(cl => cl.UserId == chattingUserId).FirstAsync();
            try
            {
                return cList.Conversations.First(c => c.ChattingUserId == uOId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal async Task BindNewUserOpenedConversation(string userId, string mobile)
        {
            var collection = Client.GetDatabase("Vessage").GetCollection<BsonDocument>("ConversationList");
            var filter1 = Builders<BsonDocument>.Filter.Eq("ForMobile", mobile);
            var filter2 = Builders<BsonDocument>.Filter.Eq("UserId", new ObjectId(userId));
            var update1 = new UpdateDefinitionBuilder<BsonDocument>().Set("UserId",new ObjectId(userId));
            var update2 = Builders<BsonDocument>.Update.Set("Conversations.$.UserId", new ObjectId(userId));
            var update = Builders<BsonDocument>.Update.Combine(update1, update2);
            await collection.UpdateManyAsync(filter1 & filter2, update);
        }

        internal async Task<Conversation> GetConversationOfReceiverMobile(string chattingUserMobile, string userId, string noteName)
        {
            var uOId = new ObjectId(userId);
            var collection = Client.GetDatabase("Vessage").GetCollection<ConversationList>("ConversationList");
            var cList = await collection.Find(cl => cl.ForMobile == chattingUserMobile).FirstAsync();
            if (cList == null)
            {
                var newClist = new ConversationList()
                {
                    Conversations = new Conversation[]
                    {
                         new Conversation()
                         {
                            ChattingUserId = uOId,
                            LastMessageDateTime = DateTime.UtcNow,
                            NoteName = noteName
                         }
                     },
                    ForMobile = chattingUserMobile
                };
                await collection.InsertOneAsync(newClist);
                return newClist.Conversations.First();
            }
            try
            {
                return cList.Conversations.First(c => c.ChattingUserId == uOId);
            }
            catch (Exception)
            {
                var conversation = new Conversation()
                {
                    ChattingUserId = uOId,
                    LastMessageDateTime = DateTime.UtcNow,
                    NoteName = noteName
                };
                var update = new UpdateDefinitionBuilder<ConversationList>().Push(cl => cl.Conversations, conversation);
                var result = await collection.UpdateOneAsync(cl => cl.ForMobile == chattingUserMobile, update);
                if (result.ModifiedCount > 0)
                {
                    return conversation;
                }
                else
                {
                    return null;
                }
            }
        }
    }

    public static class GetConversationServiceExtension
    {
        public static ConversationService GetConversationService(this IServiceProvider provider)
        {
            return provider.GetService<ConversationService>();
        }
    }
}
