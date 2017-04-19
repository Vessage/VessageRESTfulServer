using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using VessageRESTfulServer.Services;
using VessageRESTfulServer.Controllers;

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
                user = new AIViGiProfile
                {
                    CreatedTime = DateTime.UtcNow,
                    UpdatedTime = DateTime.UtcNow,
                    UserId = UserObjectId
                };
                await col.InsertOneAsync(user);

                var firstPost = new AISNSPost
                {
                    UserId = UserObjectId,
                    Body = "我今天开始使用语音助手ViGi，这是我让ViGi发布的第一条动态。",
                    BodyType = AISNSPost.BODY_TYPE_TEXT,
                    State = AISNSPost.STATE_NORMAL,
                    Type = AISNSPost.TYPE_NORMAL,
                    CreatedTime = DateTime.UtcNow,
                    UpdatedTime = DateTime.UtcNow
                };
                await AiViGiSNSDb.GetCollection<AISNSPost>("AISNSPost").InsertOneAsync(firstPost);
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