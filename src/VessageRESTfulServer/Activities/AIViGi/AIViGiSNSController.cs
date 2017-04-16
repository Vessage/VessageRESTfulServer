using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BahamutCommon;
using MongoDB.Driver;
using MongoDB.Bson;
using VessageRESTfulServer.Services;
using VessageRESTfulServer.Controllers;

namespace VessageRESTfulServer.Activities.AIViGi
{

    [Route("api/[controller]")]
    public partial class AIViGiSNSController : APIControllerBase
    {
        private IMongoDatabase AiViGiDb
        {
            get
            {
                var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
                return client.GetDatabase("AIViGi");
            }
        }

        private IMongoDatabase AiViGiSNSDb
        {
            get
            {
                var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
                return client.GetDatabase("AIViGiSNS");
            }
        }

        [HttpPost("FocusUser")]
        public async Task<object> FocusNewUserAsync(string userId, string noteName)
        {
            var col = AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus");
            var focusdUserId = new ObjectId(userId);
            var linked = await col.UpdateOneAsync(f => f.FocusedUserId == UserObjectId && f.UserId == focusdUserId, new UpdateDefinitionBuilder<AISNSFocus>().Set(p => p.Linked, true).Set(p => p.UpdatedTime, DateTime.UtcNow));

            var update = new UpdateDefinitionBuilder<AISNSFocus>()
            .Set(p => p.UpdatedTime, DateTime.UtcNow)
            .Set(p => p.FocusedNoteName, noteName)
            .Set(p => p.FocusedUserId, focusdUserId)
            .Set(p => p.Linked, linked.MatchedCount > 0);

            var r = await col.UpdateOneAsync(f => f.UserId == UserObjectId, update);
            if (r.MatchedCount == 0)
            {
                col.InsertOne(new AISNSFocus
                {
                    UserId = UserObjectId,
                    FocusedNoteName = noteName,
                    FocusedUserId = focusdUserId,
                    UpdatedTime = DateTime.UtcNow,
                    CreatedTime = DateTime.UtcNow,
                    Linked = linked.MatchedCount > 0
                });
            }

            return new
            {
                code = 200,
                msg = "SUCCESS"
            };
        }

        [HttpGet("FocusedUsers")]
        public async Task<IEnumerable<object>> GetFocusedUsersAsync()
        {
            var col = AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus");
            var list = await col.Find(f => f.UserId == UserObjectId).ToListAsync();
            return from f in list
                   select new
                   {
                       usrId = f.FocusedUserId.ToString(),
                       name = f.FocusedNoteName,
                       linked = f.Linked
                   };
        }

        [HttpGet("UsersPosts")]
        public async Task<IEnumerable<object>> GetUsersPostsAsync(string userIds)
        {
            var userIdArr = from u in userIds.Split(new char[] { ',', '#' }) select new ObjectId(u);
            var col = AiViGiSNSDb.GetCollection<AISNSPost>("AISNSPost");

            var f1 = new FilterDefinitionBuilder<AISNSFocus>().Eq(f => f.UserId, UserObjectId);
            var f2 = new FilterDefinitionBuilder<AISNSFocus>().In(f => f.FocusedUserId, userIdArr);
            var focusedUserIdArr = await AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus").Find(f1 & f2).Project(p => p.FocusedUserId).ToListAsync();

            var containFilter = new FilterDefinitionBuilder<AISNSPost>().In(f => f.UserId, focusedUserIdArr);
            var stateFilter = new FilterDefinitionBuilder<AISNSPost>().Eq(f => f.State, AISNSPost.STATE_NORMAL);
            var dateFilter = new FilterDefinitionBuilder<AISNSPost>().Gte(f => f.CreatedTime, DateTime.UtcNow.AddDays(-14));
            var filter = stateFilter & dateFilter & containFilter;
            var posts = await col.Find(filter).ToListAsync();
            return from p in posts select PostToJsonObject(p);
        }

        private object PostToJsonObject(AISNSPost post)
        {
            return new
            {
                pid = post.Id.ToString(),
                usrId = post.UserId.ToString(),
                body = post.Body,
                t = post.Type,
                st = post.State,
                cts = DateTimeUtil.UnixTimeSpanOfDateTime(post.CreatedTime).TotalMilliseconds,
                uts = DateTimeUtil.UnixTimeSpanOfDateTime(post.UpdatedTime).TotalMilliseconds,
            };
        }

        [HttpGet("AllFocusedPosts")]
        public async Task<IEnumerable<object>> GetAllPosts()
        {
            var col = AiViGiSNSDb.GetCollection<AISNSPost>("AISNSPost");
            var userIdArr = await AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus").Find(f => f.UserId == UserObjectId).Project(p => p.FocusedUserId).ToListAsync();
            var containFilter = new FilterDefinitionBuilder<AISNSPost>().In(f => f.UserId, userIdArr);
            var stateFilter = new FilterDefinitionBuilder<AISNSPost>().Eq(f => f.State, AISNSPost.STATE_NORMAL);
            var dateFilter = new FilterDefinitionBuilder<AISNSPost>().Gte(f => f.CreatedTime, DateTime.UtcNow.AddDays(-14));
            var filter = stateFilter & dateFilter & containFilter;
            var posts = await col.Find(filter).ToListAsync();
            return from p in posts select PostToJsonObject(p);
        }

        [HttpGet("RecentlyPostUsers")]
        public async Task<IEnumerable<object>> GetRecentlyPostUsers()
        {
            var col = AiViGiSNSDb.GetCollection<AISNSPost>("AISNSPost");
            var limitDate = DateTime.UtcNow.AddDays(-14);
            var focusedUserIdNames = await AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus")
            .Find(f => f.UserId == UserObjectId && f.LastPostDate > limitDate)
            .Project(p => new { id = p.FocusedUserId, name = p.FocusedNoteName })
            .ToListAsync();
            return focusedUserIdNames;
        }

        [HttpPost("NewPost")]
        public async Task PostNewAsync(string body)
        {
            var post = new AISNSPost
            {
                UserId = UserObjectId,
                Body = body,
                CreatedTime = DateTime.UtcNow,
                UpdatedTime = DateTime.UtcNow,
                Type = AISNSPost.TYPE_NORMAL,
                State = AISNSPost.STATE_NORMAL
            };
            var col = AiViGiSNSDb.GetCollection<AISNSPost>("AISNSPost");

            var update = new UpdateDefinitionBuilder<AISNSFocus>().Set(f => f.LastPostDate, DateTime.UtcNow);
            var res = await AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus").UpdateManyAsync(f => f.FocusedUserId == UserObjectId, update);
            await col.InsertOneAsync(post);
        }
    }
}