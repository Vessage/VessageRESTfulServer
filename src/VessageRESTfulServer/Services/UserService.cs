using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VessageRESTfulServer.Models;
using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using System.Linq;

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

        public async Task<IEnumerable<VessageUser>> GetUserProfilesByIds(IEnumerable<ObjectId> ids)
        {
            var collection = UserDb.GetCollection<VessageUser>("VessageUser");
            var filter = new FilterDefinitionBuilder<VessageUser>().In(f => f.Id, ids);
            return await collection.Find(filter).ToListAsync();
        }

        public async Task<IEnumerable<VessageUser>> GetActiveUsers(int limit)
        {
            var collection = UserDb.GetCollection<VessageUser>("VessageUser");
            var result = await collection.Find(f => f.AccountId != null).SortByDescending(f => f.ActiveTime).Limit(limit).ToListAsync();
            return result;
        }

        public async Task<IEnumerable<VessageUser>> GetNearUsers(ObjectId userId, GeoJson2DGeographicCoordinates geoLoc, int limit, int distance)
        {
            var collection = UserDb.GetCollection<VessageUser>("VessageUser");
            var update = new UpdateDefinitionBuilder<VessageUser>().Set(x => x.ActiveTime, DateTime.UtcNow).Set(x => x.Location, geoLoc);
            await collection.UpdateOneAsync(f => f.Id == userId, update);
            var pnt = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(geoLoc);
            var maxDis = distance;
            var filter = Builders<VessageUser>.Filter.Ne(f => f.Id, userId);
            var nearFilter = Builders<VessageUser>.Filter.NearSphere(p => p.Location, pnt, maxDis);
#if DEBUG
            var result = await collection.Find(filter).SortByDescending(f => f.ActiveTime).Limit(limit).ToListAsync();
#else    
            var result = await collection.Find(filter&nearFilter).SortByDescending(f=>f.ActiveTime).Limit(limit).ToListAsync();
#endif
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
            }
            catch (Exception)
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

        public async Task<bool> RemoveExistsNullAccountUserOfMobileAsync(string mobile, string requestByAccountId)
        {
            var collection = UserDb.GetCollection<VessageUser>("VessageUser");

            var update = new UpdateDefinitionBuilder<VessageUser>()
            .Set(p => p.Mobile, string.Format("RemovedMobile:{0}", mobile))
            .Set(p => p.AccountId, string.Format("ResetedBy:{0}", requestByAccountId));

            var res = await collection.UpdateManyAsync(u => u.Mobile == mobile && u.AccountId == null, update);
            return res.ModifiedCount > 0;
        }

        public async Task<VessageUser> BindExistsUserOnRegist(ObjectId userId, string mobile)
        {
            try
            {
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                var user = await collection.Find(u => u.Id == userId && u.Mobile == null).FirstAsync();
                if (user != null)
                {
                    var update = new UpdateDefinitionBuilder<VessageUser>()
                                                .Set(u => u.AccountId, user.AccountId)
                                                .Set(u => u.Avartar, user.Avartar)
                                                .Set(u => u.CreateTime, user.CreateTime)
                                                .Set(u => u.MainChatImage, user.MainChatImage)
                                                .Set(u => u.Mobile, mobile)
                                                .Set(u => u.Nick, user.Nick);

                    var mobileUser = await collection.FindOneAndUpdateAsync(u => u.Mobile == mobile && u.AccountId == null, update);

                    if (mobileUser != null)
                    {
                        var resetInvalidUser = new UpdateDefinitionBuilder<VessageUser>().Set(p => p.AccountId, string.Format("Reseted:{0}", user.AccountId));
                        await collection.UpdateOneAsync(u => u.Id == user.Id, resetInvalidUser);

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

        public async Task<string> GetUserNickOfUserId(ObjectId userId)
        {
            try
            {
                var collection = UserDb.GetCollection<VessageUser>("VessageUser");
                return await collection.Find(f => f.Id == userId).Project(u => u.Nick).FirstAsync();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<IEnumerable<VessageUser>> MatchUsersWithMobiles(IEnumerable<string> mobiles)
        {
            mobiles = from m in mobiles where string.IsNullOrWhiteSpace(m) == false select m;
            if (mobiles.Count() == 0)
            {
                return new VessageUser[0];
            }
            var collection = UserDb.GetCollection<VessageUser>("VessageUser");
            var filter = new FilterDefinitionBuilder<VessageUser>().In(f => f.Mobile, mobiles);
            return await collection.Find(filter).ToListAsync();
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
