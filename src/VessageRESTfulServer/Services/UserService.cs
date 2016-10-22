using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VessageRESTfulServer.Models;
using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;

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

        public async Task<VessageUser> GetUserOfUserId(ObjectId userId)
        {
            try
            {
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var user = await collection.Find(u => u.Id == userId).SingleAsync();
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

        public async Task<bool> UpdateUserActiveInfo(ObjectId userId, GeoJson2DGeographicCoordinates geoLoc)
        {

            UpdateDefinition<VessageUser> update = null;
            if (geoLoc == null)
            {
                update = new UpdateDefinitionBuilder<VessageUser>().Set(x => x.ActiveTime, DateTime.UtcNow);
            }
            else
            {
                update = new UpdateDefinitionBuilder<VessageUser>().Set(x => x.ActiveTime, DateTime.UtcNow).Set(x => x.Location, geoLoc);
            }
            var collection = UserDb.GetCollection<VessageUser>("VessageUser");
            var res = await collection.UpdateOneAsync(x => x.Id == userId, update);
            return res.ModifiedCount > 0;
        }

        public async Task<IEnumerable<VessageUser>> GetNearUsers(ObjectId userId, GeoJson2DGeographicCoordinates geoLoc)
        {
            var update = new UpdateDefinitionBuilder<VessageUser>().Set(x => x.ActiveTime, DateTime.UtcNow).Set(x => x.Location, geoLoc);
            var collection = UserDb.GetCollection<VessageUser>("VessageUser");
            var pnt = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(geoLoc);
            var maxDis = 1000 * 100;
            var filter = Builders<VessageUser>.Filter.Ne(f=>f.Id,userId);
            var nearFilter = Builders<VessageUser>.Filter.NearSphere(p => p.Location, pnt, maxDis);
            var result = await collection.Find(filter&nearFilter).SortByDescending(f=>f.ActiveTime).ToListAsync();
            await collection.UpdateOneAsync(f=>f.Id == userId,update);
            return result;
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

        public async Task<bool> ChangeMainChatImageOfUser(ObjectId userId, string image)
        {
            try
            {
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var update = new UpdateDefinitionBuilder<VessageUser>().Set(u => u.MainChatImage, image);
                var result = await collection.UpdateOneAsync(u => u.Id == userId, update);
                return result.IsModifiedCountAvailable && result.ModifiedCount > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ChangeAvatarOfUser(ObjectId userId, string avatar)
        {
            try
            {
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var update = new UpdateDefinitionBuilder<VessageUser>().Set(u => u.Avartar, avatar);
                var user = await collection.FindOneAndUpdateAsync(u => u.Id == userId, update);
                return user != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ChangeNickOfUser(ObjectId userId, string nick)
        {
            try
            {
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var update = new UpdateDefinitionBuilder<VessageUser>().Set(u => u.Nick, nick);
                var user = await collection.FindOneAndUpdateAsync(u => u.Id == userId, update);
                return user != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<VessageUser> BindExistsUserOnRegist(ObjectId userId, string mobile)
        {
            try
            {
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var user = await collection.Find(u => u.Id == userId && u.Mobile == null).FirstAsync();
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

        public async Task<bool> UpdateMobileOfUser(ObjectId userId, string mobile)
        {
            try
            {
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var update = new UpdateDefinitionBuilder<VessageUser>().Set(u => u.Mobile, mobile);
                var user = await collection.FindOneAndUpdateAsync(u => u.Id == userId, update);
                return user != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<IEnumerable<ChatImageInfo>> GetUserChatImages(ObjectId userId)
        {
            var collection = UserDb.GetCollection<ChatImageInfo>("UserChatImage");
            var result = await collection.Find(f => f.UserId == userId).ToListAsync();
            return result;
        }

        public async Task<bool> UpdateChatImageOfUser(ObjectId userId, string image, string imageType)
        {
            try
            {
                var collection = UserDb.GetCollection<ChatImageInfo>("UserChatImage");
                var update = new UpdateDefinitionBuilder<ChatImageInfo>().Set(f => f.ImageFileId, image);
                var result = await collection.UpdateOneAsync(ci => ci.UserId == userId && ci.ImageType == imageType, update);
                if (!result.IsModifiedCountAvailable || result.ModifiedCount == 0)
                {
                   var newChatImage = new ChatImageInfo
                    {
                        UserId = userId,
                        ImageFileId = image,
                        ImageType = imageType
                    };
                    await collection.InsertOneAsync(newChatImage);
                    return newChatImage.Id != ObjectId.Empty;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ChangeSexValue(ObjectId userId, int value)
        {
            try
            {
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var update = new UpdateDefinitionBuilder<VessageUser>().Set(u => u.Sex, value);
                var res = await collection.UpdateOneAsync(u => u.Id == userId, update);
                return res.ModifiedCount > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ChangeMottoOfUser(ObjectId userObjectId, string motto)
        {
            try
            {
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var update = new UpdateDefinitionBuilder<VessageUser>().Set(u => u.Motto, motto);
                var res = await collection.UpdateOneAsync(u => u.Id == userObjectId, update);
                return res.ModifiedCount > 0;
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
