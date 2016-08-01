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
        private IMongoDatabase UserDb { get { return Client.GetDatabase("Vessage"); } }

        public UserService(IMongoClient Client)
        {
            this.Client = Client;
        }

        public async Task<VessageUser> GetUserOfAccountId(string accountId)
        {
            try
            {
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
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
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var userOId = new ObjectId(userId);
                var user = await collection.Find(u => u.Id == userOId).SingleAsync();
                return user;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<VessageUser> CreateNewUserByMobile(string mobile)
        {
            
            try
            {
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var user = new VessageUser
                {
                    CreateTime = DateTime.UtcNow,
                    Mobile = mobile
                };
                await collection.InsertOneAsync(user);
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
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
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
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var user = await collection.Find(u => u.Mobile == mobile).FirstAsync();
                return user;
            }
            catch (InvalidOperationException)
            {
                return null;
            }catch (Exception)
            {
                throw;
            }
        }

        public async Task<bool> ChangeMainChatImageOfUser(string userId, string image)
        {
            try
            {
                var userOId = new ObjectId(userId);
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var update = new UpdateDefinitionBuilder<VessageUser>().Set(u => u.MainChatImage, image);
                var result = await collection.UpdateOneAsync(u => u.Id == userOId, update);
                return result.IsModifiedCountAvailable && result.ModifiedCount > 0;
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
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
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
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var update = new UpdateDefinitionBuilder<VessageUser>().Set(u => u.Nick, nick);
                var user = await collection.FindOneAndUpdateAsync(u => u.Id == userOId, update);
                return user != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<VessageUser> BindExistsUserOnRegist(string userId, string mobile)
        {
            try
            {
                var userOId = new ObjectId(userId);
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var user = await collection.Find(u => u.Id == userOId && u.Mobile == null).FirstAsync();
                if (user != null)
                {
                    var mobileUser = await collection.Find(u => u.Mobile == mobile && u.AccountId == null).FirstAsync();
                    var update = new UpdateDefinitionBuilder<VessageUser>()
                        .Set(u => u.AccountId, user.AccountId)
                        .Set(u => u.Avartar, user.Avartar)
                        .Set(u => u.CreateTime, user.CreateTime)
                        .Set(u => u.MainChatImage, user.MainChatImage)
                        .Set(u => u.Mobile, mobile)
                        .Set(u => u.Nick, user.Nick);
                    mobileUser = await collection.FindOneAndUpdateAsync(u => u.Id == mobileUser.Id, update);
                    await collection.DeleteOneAsync(u => u.Id == user.Id);
                    if (mobileUser != null)
                    {
                        return mobileUser;
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public async Task<bool> UpdateMobileOfUser(string userId, string mobile)
        {
            try
            {
                var userOId = new ObjectId(userId);
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var update = new UpdateDefinitionBuilder<VessageUser>().Set(u => u.Mobile, mobile);
                var user = await collection.FindOneAndUpdateAsync(u => u.Id == userOId, update);
                return user != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<IEnumerable<ChatImageInfo>> GetUserChatImages(ObjectId userObjectId)
        {
            var collection = UserDb.GetCollection<ChatImageInfo>("UserChatImage");
            var result = await collection.Find(f => f.UserId == userObjectId).ToListAsync();
            return result;
        }

        public async Task<bool> UpdateChatImageOfUser(string userId, string image, string imageType)
        {
            try
            {
                var userOId = new ObjectId(userId);
                var collection = UserDb.GetCollection<ChatImageInfo>("UserChatImage");
                var update = new UpdateDefinitionBuilder<ChatImageInfo>().Set(f => f.ImageFileId, image);
                var newChatImage = await collection.FindOneAndUpdateAsync<ChatImageInfo>(ci => ci.UserId == userOId && ci.ImageType == imageType, update);
                if (newChatImage == null)
                {
                    newChatImage = new ChatImageInfo
                    {
                        UserId = userOId,
                        ImageFileId = image,
                        ImageType = imageType
                    };
                    await collection.InsertOneAsync(newChatImage);
                    return newChatImage.Id != ObjectId.Empty;
                }
                return newChatImage.ImageFileId == image;
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
