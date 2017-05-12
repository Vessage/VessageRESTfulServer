using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using VessageRESTfulServer.Services;
using VessageRESTfulServer.Controllers;
using MongoDB.Bson;

namespace VessageRESTfulServer.Activities.AIViGi
{

    [Route("api/[controller]")]
    public partial class AIViGiController : APIControllerBase
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

        private static AISNSFocus[] DefaultFocusProfiles = {
            new AISNSFocus{ FocusedNoteName = "语音助手公告",FocusedUserId = new ObjectId("589576a736c14122b8b8f3b8"),NotificationState = AISNSFocus.NOTIFICATION_STATE_ON },
            new AISNSFocus{ FocusedNoteName = "账号推荐",FocusedUserId = new ObjectId("590f352f0d7d036859bf0e82"),NotificationState = AISNSFocus.NOTIFICATION_STATE_ON }
        };

        private static AISNSPost[] DefaultPosts = {
            new AISNSPost{ Body = "这是为新用户自动发布的一条动态，欢迎使用ViGi。ps:你可以左划删除这条动态。",BodyType = AISNSPost.BODY_TYPE_TEXT }
        };

        [HttpGet("AIProfile")]
        public async Task<object> GetUserSettingInfoAsync()
        {
            var col = AiViGiDb.GetCollection<AIViGiProfile>("AIViGiProfile");
            var user = await col.Find(f => f.UserId == UserObjectId).FirstOrDefaultAsync();
            if (user == null)
            {
                var userOId = UserObjectId;
                var userAccount = UserSessionData.AccountId;
                var now = DateTime.UtcNow;

                user = new AIViGiProfile
                {
                    CreatedTime = now,
                    UpdatedTime = now,
                    AccountId = userAccount,
                    UserId = userOId
                };
                await col.InsertOneAsync(user);

                var posts = from p in DefaultPosts
                            select new AISNSPost
                            {
                                UserId = userOId,
                                Body = p.Body,
                                BodyType = p.BodyType,
                                State = AISNSPost.STATE_NORMAL,
                                Type = AISNSPost.TYPE_NORMAL,
                                CreatedTime = now,
                                UpdatedTime = now
                            };
                await AiViGiSNSDb.GetCollection<AISNSPost>("AISNSPost").InsertManyAsync(posts);



                var focus = from f in DefaultFocusProfiles
                            select new AISNSFocus
                            {
                                UserId = userOId,
                                UserAccount = userAccount,
                                UserNick = userAccount,
                                FocusedNoteName = f.FocusedNoteName,
                                FocusedUserId = f.FocusedUserId,
                                UpdatedTime = now,
                                CreatedTime = now,
                                Linked = false,
                                State = AISNSFocus.STATE_NORMAL,
                                LastPostDate = now,
                                NotificationState = f.NotificationState
                            };
                await AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus").InsertManyAsync(focus);
            }

            return new
            {
                id = user.Id.ToString(),
                masterName = user.MasterName
            };
        }

        [HttpPut("MasterName")]
        public async Task<object> UpdateMasterNameAsync(string newName)
        {
            var update = new UpdateDefinitionBuilder<AIViGiProfile>().Set(p => p.MasterName, newName).Set(p => p.UpdatedTime, DateTime.UtcNow);
            var col = AiViGiDb.GetCollection<AIViGiProfile>("AIViGiProfile");
            var res = await col.UpdateOneAsync(f => f.UserId == UserObjectId, update);
            if (res.MatchedCount > 0)
            {
                return new
                {
                    code = 200,
                    msg = "SUCCESS"
                };
            }
            else
            {
                Response.StatusCode = 400;
                return new
                {
                    code = 400,
                    msg = "NOT_FOUND"
                };
            }
        }
    }
}