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

namespace VessageRESTfulServer.Activities.MNS
{
    public class MNSConfigCenter
    {
        public const string ActivityId = "1004";
        private static IConfiguration _mnsConfig;
        private static IConfiguration MNSConfig
        {
            get
            {
                if (_mnsConfig == null)
                {
                    var configRoot = Startup.ConfigRoot + Path.DirectorySeparatorChar;
                    _mnsConfig = new ConfigurationBuilder()
                        .AddJsonFile(string.Format("{0}mns_config.json", configRoot), true, true)
                        .Build();
                }
                return _mnsConfig;
            }
        }
    }

    [Route("api/[controller]")]
    public partial class MNSController : APIControllerBase
    {
        private IMongoDatabase MNSDb
        {
            get
            {
                var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
                return client.GetDatabase("MNS");
            }
        }

        [HttpGet("MainInfo")]
        public async Task<object> GetMainInfo(string location = null)
        {
            var usrCol = MNSDb.GetCollection<MNSProfile>("MNSProfile");

            var isNewer = false;
            MNSProfile profile = null;
            try
            {
                var filter = new FilterDefinitionBuilder<MNSProfile>().Where(f => f.UserId == UserObjectId);
                var update = new UpdateDefinitionBuilder<MNSProfile>().Set(p => p.ActiveTime, DateTime.UtcNow);

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
                profile = new MNSProfile
                {
                    UserId = UserObjectId,
                    ActiveTime = DateTime.UtcNow,
                    CreateTime = DateTime.UtcNow,
                    ProfileState = MNSProfile.STATE_NORMAL,
                    Nick = myUserProfile.Nick,
                    Avatar = myUserProfile.Avartar,
                    Location = string.IsNullOrWhiteSpace(location) ? null : Utils.LocationStringToLocation(location)
                };
                await usrCol.InsertOneAsync(profile);
                isNewer = true;
            }
            var now = DateTime.UtcNow;
            var limit = now.AddHours(-1);
            IEnumerable<MNSProfile> profiles = await usrCol.Find(p => p.UserId != UserObjectId && p.ProfileState == MNSProfile.STATE_NORMAL && p.ActiveTime > limit).SortByDescending(p=>p.ActiveTime).Limit(100).ToListAsync();

            return new
            {
                newer = isNewer,
                annc = profile.MidNightAnnounce,
                acUsers = from p in profiles select MNSProfileToJsonObject(p)
            };
        }

        [HttpPut("MidNightAnnc")]
        public async Task<object> UpdateMidNightAnncAsync(string mnannc)
        {
            var usrCol = MNSDb.GetCollection<MNSProfile>("MNSProfile");
            var update = new UpdateDefinitionBuilder<MNSProfile>()
            .Set(p => p.ActiveTime, DateTime.UtcNow)
            .Set(p => p.MidNightAnnounce, mnannc);
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

        private object MNSProfileToJsonObject(MNSProfile p)
        {
            return new
            {
                userId = p.UserId.ToString(),
                nick = p.Nick,
                annc = p.MidNightAnnounce,
                avatar = p.Avatar,
                aTs = (long)DateTimeUtil.UnixTimeSpanOfDateTime(p.ActiveTime).TotalMilliseconds
            };
        }
    }
}