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
    public class UserService
    {
        protected IMongoClient Client { get; set; }
        public UserService(IMongoClient Client)
        {
            this.Client = Client;
        }
        public async Task<VessageUser> GetUserOfAccountId(string accountId)
        {
            try
            {
                var collection = Client.GetDatabase("Vessage").GetCollection<VessageUser>("VessageUser");
                var user = await collection.Find(u => u.AccountId == accountId).SingleAsync();
                return user;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<VessageUser> GetUserOfUserId(string userId)
        {
            try
            {
                var collection = Client.GetDatabase("Vessage").GetCollection<VessageUser>("VessageUser");
                var userOId = new ObjectId(userId);
                var user = await collection.Find(u => u.Id == userOId).SingleAsync();
                return user;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<VessageUser> CreateNewUser(VessageUser newUser)
        {
            try
            {
                var collection = Client.GetDatabase("Vessage").GetCollection<VessageUser>("VessageUser");
                await collection.InsertOneAsync(newUser);
                return newUser;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<VessageUser> GetUserOfMobile(string mobile)
        {
            try
            {
                var collection = Client.GetDatabase("Vessage").GetCollection<VessageUser>("VessageUser");
                var user = await collection.Find(u => u.Mobile == mobile).SingleAsync();
                return user;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> ChangeMainChatImageOfUser(string userId, string image)
        {
            try
            {
                var userOId = new ObjectId(userId);
                var collection = Client.GetDatabase("Vessage").GetCollection<VessageUser>("VessageUser");
                var update = new UpdateDefinitionBuilder<VessageUser>().Set(u => u.MainChatImage, image);
                var user = await collection.FindOneAndUpdateAsync(u => u.Id == userOId, update);
                return user != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ChangeAvatarOfUser(string userId, string avatar)
        {
            try
            {
                var userOId = new ObjectId(userId);
                var collection = Client.GetDatabase("Vessage").GetCollection<VessageUser>("VessageUser");
                var update = new UpdateDefinitionBuilder<VessageUser>().Set(u => u.Avartar, avatar);
                var user = await collection.FindOneAndUpdateAsync(u => u.Id == userOId, update);
                return user != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ChangeNickOfUser(string userId, string nick)
        {
            try
            {
                var userOId = new ObjectId(userId);
                var collection = Client.GetDatabase("Vessage").GetCollection<VessageUser>("VessageUser");
                var update = new UpdateDefinitionBuilder<VessageUser>().Set(u => u.Nick, nick);
                var user = await collection.FindOneAndUpdateAsync(u => u.Id == userOId, update);
                return user != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> UpdateMobileOfUser(string userId, string mobile)
        {
            try
            {
                var userOId = new ObjectId(userId);
                var collection = Client.GetDatabase("Vessage").GetCollection<VessageUser>("VessageUser");
                var update = new UpdateDefinitionBuilder<VessageUser>().Set(u => u.Mobile, mobile);
                var user = await collection.FindOneAndUpdateAsync(u => u.Id == userOId, update);
                return user != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public static class GetUserServiceExtension
    {
        public static UserService GetUserService(this IServiceProvider provider)
        {
            return provider.GetService<UserService>();
        }
    }
}
