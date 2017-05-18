using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using VessageRESTfulServer.Models;
using MongoDB.Driver;

namespace VessageRESTfulServer.Services
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class GroupChatService
    {
        protected IMongoClient Client { get; set; }
        private IMongoDatabase VessageDb { get { return Client.GetDatabase("VessageChatGroup"); } }

        public GroupChatService(IMongoClient Client)
        {
            this.Client = Client;
        }

        public async Task<ChatGroup> CreateChatGroup(ObjectId hoster, IEnumerable<ObjectId> chatters, string groupName)
        {
            var chatterList = new List<ObjectId>(chatters);
            if (!chatterList.Contains(hoster))
            {
                chatterList.Insert(0, hoster);
            }
            var group = new ChatGroup
            {
                GroupName = groupName,
                Chatters = chatterList.ToArray(),
                Hosters = new ObjectId[] { hoster },
                InviteCode = new Random(DateTime.Now.Millisecond).Next(1000, 9999).ToString()
            };
            var collection = VessageDb.GetCollection<ChatGroup>("ChatGroup");
            await collection.InsertOneAsync(group);
            return group;
        }

        public async Task<bool> UserJoinGroup(ObjectId userId, ObjectId groupId, string inviteCode)
        {
            var collection = VessageDb.GetCollection<ChatGroup>("ChatGroup");
            var update = new UpdateDefinitionBuilder<ChatGroup>().AddToSet(g => g.Chatters, userId);
            var result = await collection.UpdateOneAsync(f => f.Id == groupId && f.InviteCode == inviteCode && !f.Chatters.Contains(userId), update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> AddUserJoinGroup(ObjectId hoster, ObjectId groupId, ObjectId userId)
        {
            var collection = VessageDb.GetCollection<ChatGroup>("ChatGroup");
            var update = new UpdateDefinitionBuilder<ChatGroup>().AddToSet(g => g.Chatters, userId);
            var result = await collection.UpdateOneAsync(f => f.Id == groupId && f.Hosters.Contains(hoster) && !f.Chatters.Contains(userId), update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> QuitChatGroup(ObjectId userId, ObjectId groupId)
        {
            var collection = VessageDb.GetCollection<ChatGroup>("ChatGroup");
            var g = await collection.Find(f => f.Id == groupId).FirstOrDefaultAsync();
            if (g != null)
            {
                return await KickUserFromChatGroup(collection, g.Hosters[0], groupId, userId);
            }
            return false;
        }

        public async Task<bool> KickUserFromChatGroup(ObjectId hoster, ObjectId groupId, ObjectId kickUserId)
        {
            var collection = VessageDb.GetCollection<ChatGroup>("ChatGroup");
            return await KickUserFromChatGroup(collection, hoster, groupId, kickUserId);
        }

        public async Task<ChatGroup> GetChatGroupById(ObjectId userId, ObjectId groupId)
        {
            var collection = VessageDb.GetCollection<ChatGroup>("ChatGroup");
            try
            {
                return await collection.Find(f => f.Id == groupId && f.Chatters.Contains(userId)).FirstAsync();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<bool> KickUserFromChatGroup(IMongoCollection<ChatGroup> collection, ObjectId hoster, ObjectId groupId, ObjectId kickUserId)
        {
            var update = new UpdateDefinitionBuilder<ChatGroup>().Pull(g => g.Chatters, kickUserId).Pull(g => g.Hosters, kickUserId);
            var option = new FindOneAndUpdateOptions<ChatGroup>()
            {
                ReturnDocument = ReturnDocument.After
            };

            var filter1 = new FilterDefinitionBuilder<ChatGroup>().Eq(c => c.Id, groupId);
            var filter2 = new FilterDefinitionBuilder<ChatGroup>().AnyEq(c => c.Hosters, hoster);

            var result = await collection.FindOneAndUpdateAsync(filter1 & filter2, update, option);
            if (result.Hosters.Count() == 0 && result.Chatters.Count() > 0)
            {
                update = new UpdateDefinitionBuilder<ChatGroup>().AddToSet(g => g.Hosters, result.Chatters[0]);
                await collection.UpdateOneAsync(g => g.Id == groupId, update);
            }
            return result.Id == groupId;
        }

        public async Task<bool> EditGroupName(ObjectId groupId, string inviteCode, string newGroupName)
        {
            var update = new UpdateDefinitionBuilder<ChatGroup>().Set(f => f.GroupName, newGroupName);
            var collection = VessageDb.GetCollection<ChatGroup>("ChatGroup");
            var result = await collection.UpdateOneAsync(f => f.Id == groupId && f.InviteCode == inviteCode, update);
            return result.ModifiedCount > 0;
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class GroupChatServiceExtensions
    {
        public static GroupChatService GetGroupChatService(this IServiceProvider provider)
        {
            return provider.GetService<GroupChatService>();
        }
    }
}
