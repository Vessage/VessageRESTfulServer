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