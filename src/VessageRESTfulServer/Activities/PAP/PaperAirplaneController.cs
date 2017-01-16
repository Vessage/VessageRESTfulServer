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

namespace VessageRESTfulServer.Activities.PAP
{
    public class PaperAirplaneConfig
    {
        public const string ActivityId = "1007";
        private static IConfiguration _PAPConfig;
        private static IConfiguration PAPConfig
        {
            get
            {
                if (_PAPConfig == null)
                {
                    var configRoot = Startup.ConfigRoot + Path.DirectorySeparatorChar;
                    _PAPConfig = new ConfigurationBuilder()
                        .AddJsonFile(string.Format("{0}PAP_config.json", configRoot), true, true)
                        .Build();
                }
                return _PAPConfig;
            }
        }
    }

    [Route("api/[controller]")]
    public partial class PaperAirplaneController : APIControllerBase
    {
        private IMongoDatabase PAPDb
        {
            get
            {
                var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
                return client.GetDatabase("PaperAirplane");
            }
        }

        [HttpPost("New")]
        public async Task<object> NewPaperAirplane(string nick, string avatar, string msg, string location = null)
        {
            var p = new PaperAirplane
            {
                Messages = new PaperAirplaneMessage[]
                {
                    new PaperAirplaneMessage{
                        UserId = UserObjectId,
                        Nick = nick,
                        Avatar = avatar,
                        Content = msg,
                        CreateTime = DateTime.UtcNow,
                        Location = string.IsNullOrWhiteSpace(location) ? null : Utils.LocationStringToLocation(location)
                    }
                 },
                CreateTime = DateTime.UtcNow,
                UpdatedTime = DateTime.UtcNow,
                Owner = UserObjectId,
                State = PaperAirplane.STATE_FLYING
            };
            var col = PAPDb.GetCollection<PaperAirplane>("PaperAirplane");
            await col.InsertOneAsync(p);
            if (p.Id != ObjectId.Empty)
            {
                return new { msg = "SUCCESS" };
            }
            else
            {
                Response.StatusCode = 500;
                return new { msg = "ERROR" };
            }

        }

        [HttpPost("New")]
        public async Task<object> NewPaperAirplane(string paId, string nick, string avatar, string msg, string location = null)
        {
            var planeId = new ObjectId(paId);
            var newMsg = new PaperAirplaneMessage
            {
                UserId = UserObjectId,
                Nick = nick,
                Avatar = avatar,
                Content = msg,
                CreateTime = DateTime.UtcNow,
                Location = string.IsNullOrWhiteSpace(location) ? null : Utils.LocationStringToLocation(location)
            };
            var col = PAPDb.GetCollection<PaperAirplane>("PaperAirplane");

            var update = new UpdateDefinitionBuilder<PaperAirplane>()
            .Push(a => a.Messages, newMsg)
            .Set(a => a.State, PaperAirplane.STATE_FLYING)
            .Set(a => a.UpdatedTime, DateTime.UtcNow);

            var result = await col.UpdateOneAsync(f => f.Id == planeId, update);

            if (result.ModifiedCount > 0)
            {
                return new { msg = "SUCCESS" };
            }
            else
            {
                Response.StatusCode = 500;
                return new { msg = "ERROR" };
            }

        }

        [HttpPost("Box")]
        public async Task<IEnumerable<object>> GetMyBoxPlanes()
        {
            var col = PAPDb.GetCollection<PaperAirplane>("PaperAirplane");
            var result = await col.Find(f => f.Owner == UserObjectId && f.State == PaperAirplane.STATE_OWNER_KEEPING).SortByDescending(f => f.UpdatedTime).ToListAsync();
            return from p in result select PaperAirplaneToJsonObject(p);
        }

        private object PaperAirplaneToJsonObject(PaperAirplane p)
        {
            return new
            {
                id = p.Id.ToString(),
                msgs = from m in p.Messages select PaperAirplaneMessageToJsonObject(m)
            };
        }

        static private object PaperAirplaneMessageToJsonObject(PaperAirplaneMessage m)
        {
            return new
            {
                usrId = m.UserId.ToString(),
                nick = m.Nick,
                avatar = m.Avatar,
                msg = m.Content,
                ts = DateTimeUtil.UnixTimeSpanOfDateTime(m.CreateTime).TotalMilliseconds,
                loc = m.Location == null ? null : new double[] { m.Location.Longitude, m.Location.Latitude },
            };
        }

        [HttpDelete]
        public async Task<object> DeletePaperAirplane(string paId)
        {
            var planeId = new ObjectId(paId);
            var col = PAPDb.GetCollection<PaperAirplane>("PaperAirplane");

            var update = new UpdateDefinitionBuilder<PaperAirplane>()
            .Set(a => a.State, PaperAirplane.STATE_DESTROIED)
            .Set(a => a.UpdatedTime, DateTime.UtcNow);

            var result = await col.UpdateOneAsync(f => f.Id == planeId && f.Owner == UserObjectId, update);

            if (result.ModifiedCount > 0)
            {
                return new { msg = "SUCCESS" };
            }
            else
            {
                Response.StatusCode = 500;
                return new { msg = "ERROR" };
            }

        }

        [HttpPost("Catch")]
        public async Task<object> CatchPaperAirplane()
        {

            var col = PAPDb.GetCollection<PaperAirplane>("PaperAirplane");

            var update = new UpdateDefinitionBuilder<PaperAirplane>()
            .Set(a => a.State, PaperAirplane.STATE_OWNER_KEEPING)
            .Set(a => a.Owner, UserObjectId)
            .Set(a => a.UpdatedTime, DateTime.UtcNow);

            var result = await col.FindOneAndUpdateAsync(f => f.State == PaperAirplane.STATE_FLYING, update);

            if (result != null)
            {
                return PaperAirplaneToJsonObject(result);
            }else{
                Response.StatusCode = 400;
                return null;
            }
        }

    }
}