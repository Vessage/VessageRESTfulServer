using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VessageRESTfulServer.Models;

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

        internal Task<VessageUser> GetUserOfUserId(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<VessageUser> CreateNewUser(VessageUser newUser)
        {
            throw new NotImplementedException();
        }

        internal Task<VessageUser> GetUserOfMobile(string mobile)
        {
            throw new NotImplementedException();
        }

        internal Task<bool> ChangeMainChatImageOfUser(string userId, string image)
        {
            throw new NotImplementedException();
        }

        internal Task<bool> ChangeAvatarOfUser(string userId, string avatar)
        {
            throw new NotImplementedException();
        }

        internal Task<bool> ChangeNickOfUser(string userId, string nick)
        {
            throw new NotImplementedException();
        }

        internal Task<bool> UpdateMobileOfUser(string userId, string mobile)
        {
            throw new NotImplementedException();
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
