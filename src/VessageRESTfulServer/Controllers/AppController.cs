using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VessageRESTfulServer.Services;
using MongoDB.Bson;
using BahamutService.Service;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VessageRESTfulServer.Models;
using Microsoft.Extensions.Configuration;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{
    [Route("api/[controller]")]
    public class AppController : APIControllerBase
    {
        private static Dictionary<string,JObject> VersionVessage = new Dictionary<string, JObject>();

        [HttpPost("FirstLaunch")]
        public async Task Post(string platform, int buildVersion,int oldBuildVersion)
        {
            try
            {
                if (!await SendVersionVessages(UserObjectId, platform, buildVersion, oldBuildVersion))
                {
                    Response.StatusCode = 403;
                }
            }
            catch (Exception)
            {
                Response.StatusCode = 403;
            }
        }

        public static JObject LoadVersionVessageConfig(string platform, int buildVersion)
        {
            var key = string.Format("{0}_{1}", platform, buildVersion);
            try
            {
                var result = VersionVessage[key];
                return result;
            }
            catch (Exception)
            {
                try
                {
                    var configRoot = Startup.Configuration["Data:ConfigRoot"];
                    var path = string.Format("{0}/start_up_vessage_{1}.json", configRoot, key);
                    var jsonObj = System.IO.File.ReadAllText(path);
                    var jobj = JObject.Parse(jsonObj);
                    VersionVessage[key] = jobj;
                    return jobj;
                }
                catch (Exception e)
                {
                    NLog.LogManager.GetLogger("Warn").Warn("Load Platform {0} Version {1} Hello Vessages Exception:{2}", platform, buildVersion, e.Message);
                }
            }
            return null;
        }

        private async Task<bool> SendVersionVessages(ObjectId UserId,string platform, int buildVersion, int oldBuildVersion)
        {
            var jsonObj = LoadVersionVessageConfig(platform, buildVersion);
            if (jsonObj == null)
            {
                return false;
            }
            JArray jsonArr = null;
            if (oldBuildVersion == 0)
            {
                jsonArr = jsonObj["vessages"]["install"] as JArray;
            }
            else
            {
                jsonArr = jsonObj["vessages"]["upgrade"] as JArray;
            }

            var now = DateTime.UtcNow;
            var i = 0;
            var vsgs = from JObject u in jsonArr
                       select new Vessage
                       {
                           Body = u["Body"].ToObject<string>(),
                           ExtraInfo = u["ExtraInfo"].ToObject<string>(),
                           Id = ObjectId.GenerateNewId(),
                           IsGroup = u["IsGroup"].ToObject<bool>(),
                           IsRead = u["IsRead"].ToObject<bool>(),
                           Sender = new ObjectId(u["Sender"].ToObject<string>()),
                           SendTime = now.AddMilliseconds(i++),
                           TypeId = u["TypeId"].ToObject<int>(),
                           Video = u["Video"].ToObject<string>(),
                           VideoReady = u["VideoReady"].ToObject<bool>()
                       };
            if (vsgs.Count() == 0)
            {
                return true;
            }
            var id = await AppServiceProvider.GetVessageService().SendVessagesToUser(UserId, vsgs);
            if (id != ObjectId.Empty)
            {
                var notifyMsg = new BahamutPublishModel
                {
                    NotifyInfo = JsonConvert.SerializeObject(new
                    {
                        BuilderId = 1,
                        AfterOpen = "go_custom",
                        Custom = "NewVessageNotify",
                        Text = "",
                        LocKey = "NEW_VMSG_NOTIFICATION"
                    }, Formatting.None),
                    NotifyType = "NewVessageNotify",
                    ToUser = UserId.ToString()
                };
                AppServiceProvider.GetBahamutPubSubService().PublishVegeNotifyMessage(notifyMsg);
            }
            return true;
        }
    }
}
