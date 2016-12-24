using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BahamutCommon;
using MongoDB.Driver;
using MongoDB.Bson;
using VessageRESTfulServer.Services;
using Microsoft.Extensions.Configuration;
using System.IO;
using VessageRESTfulServer.Controllers;
using System.Net;

namespace VessageRESTfulServer.Activities.MYQ
{
    public class MYQConfigCenter
    {
        public const string ActivityId = "1006";
        private static IConfiguration _MYQConfig;
        private static IConfiguration MYQConfig
        {
            get
            {
                if (_MYQConfig == null)
                {
                    var configRoot = Startup.ConfigRoot + Path.DirectorySeparatorChar;
                    _MYQConfig = new ConfigurationBuilder()
                        .AddJsonFile(string.Format("{0}myq_config.json", configRoot), true, true)
                        .Build();
                }
                return _MYQConfig;
            }
        }
    }

    [Route("api/[controller]")]
    public partial class MYQController : APIControllerBase
    {
        private IMongoDatabase MYQDb
        {
            get
            {
                var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
                return client.GetDatabase("MYQ");
            }
        }

        [HttpGet("MainInfo")]
        public async Task<object> GetMainInfo(string location = null)
        {
            var usrCol = MYQDb.GetCollection<MYQProfile>("MYQProfile");

            var isNewer = false;
            MYQProfile profile = null;
            try
            {
                var filter = new FilterDefinitionBuilder<MYQProfile>().Where(f => f.UserId == UserObjectId);
                var update = new UpdateDefinitionBuilder<MYQProfile>().Set(p => p.ActiveTime, DateTime.UtcNow);

                if (!string.IsNullOrWhiteSpace(location))
                {
                    var c = Utils.LocationStringToLocation(location);
                    update = update.Set(p => p.Location, c);
                }
                profile = await usrCol.FindOneAndUpdateAsync(filter, update);
                if (profile == null || profile.Id == ObjectId.Empty)
                {
                    throw new NullReferenceException();
                }
            }
            catch (NullReferenceException)
            {
                var myUserProfile = await AppServiceProvider.GetUserService().GetUserOfUserId(UserObjectId);
                profile = new MYQProfile
                {
                    UserId = UserObjectId,
                    ActiveTime = DateTime.UtcNow,
                    CreateTime = DateTime.UtcNow,
                    ProfileState = MYQProfile.STATE_NORMAL,
                    Nick = myUserProfile.Nick,
                    Avatar = myUserProfile.Avartar,
                    Location = string.IsNullOrWhiteSpace(location) ? null : Utils.LocationStringToLocation(location)
                };
                await usrCol.InsertOneAsync(profile);
                await AppServiceProvider.GetActivityService().CreateActivityBadgeData(MYQConfigCenter.ActivityId,UserObjectId);
                isNewer = true;
            }
            IEnumerable<MYQProfile> profiles = await usrCol.Find(p =>p.ProfileState == MYQProfile.STATE_NORMAL && p.Question != null).SortByDescending(p=>p.ActiveTime).Limit(100).ToListAsync();

            return new
            {
                newer = isNewer,
                ques = profile.Question,
                usrQues = from p in profiles select MYQProfileToJsonObject(p)
            };
        }

        [HttpPut("Question")]
        public async Task<object> UpdateQuestionAsync(string ques)
        {
            var usrCol = MYQDb.GetCollection<MYQProfile>("MYQProfile");
            var update = new UpdateDefinitionBuilder<MYQProfile>()
            .Set(p => p.ActiveTime, DateTime.UtcNow)
            .Set(p => p.Question, string.IsNullOrWhiteSpace(ques) ? null : ques);
            var r = await usrCol.UpdateOneAsync(f => f.UserId == UserObjectId, update);
            if (r.ModifiedCount > 0 || r.MatchedCount > 0)
            {
                return new { msg = "SUCCESS" };
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.NotModified;
                return new { msg = "NOT_MODIFIED" };
            }
        }

        private object MYQProfileToJsonObject(MYQProfile p)
        {
            return new
            {
                userId = p.UserId.ToString(),
                nick = p.Nick,
                ques = p.Question,
                avatar = p.Avatar,
                aTs = (long)DateTimeUtil.UnixTimeSpanOfDateTime(p.ActiveTime).TotalMilliseconds
            };
        }
    }
}