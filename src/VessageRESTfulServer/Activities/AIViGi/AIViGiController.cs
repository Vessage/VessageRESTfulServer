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

        [HttpGet("AIProfile")]
        public async Task<object> GetUserSettingInfoAsync()
        {
            var col = AiViGiDb.GetCollection<AIViGiProfile>("AIViGiProfile");
            var user = await col.Find(f => f.UserId == UserObjectId).FirstOrDefaultAsync();
            if (user == null)
            {
                var userOId = UserObjectId;
                var now = DateTime.UtcNow;

                user = new AIViGiProfile
                {
                    CreatedTime = now,
                    UpdatedTime = now,
                    UserId = userOId
                };
                await col.InsertOneAsync(user);

                var firstPost = new AISNSPost
                {
                    UserId = userOId,
                    Body = "我今天开始使用语音助手ViGi，这是我让ViGi发布的第一条动态。",
                    BodyType = AISNSPost.BODY_TYPE_TEXT,
                    State = AISNSPost.STATE_NORMAL,
                    Type = AISNSPost.TYPE_NORMAL,
                    CreatedTime = now,
                    UpdatedTime = now
                };
                await AiViGiSNSDb.GetCollection<AISNSPost>("AISNSPost").InsertOneAsync(firstPost);

                var userAccount = UserSessionData.AccountId;

                var newFocus = new AISNSFocus
                {
                    UserId = userOId,
                    UserAccount = userAccount,
                    UserNick = userAccount,
                    FocusedNoteName = "语音助手公告",
                    FocusedUserId = new ObjectId("589576a736c14122b8b8f3b8"),
                    UpdatedTime = now,
                    CreatedTime = now,
                    Linked = false,
                    State = AISNSFocus.STATE_NORMAL,
                    LastPostDate = now
                };

                await AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus").InsertOneAsync(newFocus);
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