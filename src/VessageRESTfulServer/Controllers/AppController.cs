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

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{
    [Route("api/[controller]")]
    public class AppController : APIControllerBase
    {
        [HttpPost("FirstLaunch")]
        public async Task Post(string platform, int buildVersion)
        {
            try
            {
                if (!await SendFirstLaunchVessage(UserObjectId, platform, buildVersion))
                {
                    Response.StatusCode = 403;
                }
            }
            catch (Exception)
            {
                Response.StatusCode = 403;
            }
        }

        private static Dictionary<string,JArray> FirstLaunchVessages = new Dictionary<string,JArray>();
        public static JArray Get1stLaunchVessages(string platform, int buildVersion)
        {
            var key = string.Format("{0}_{1}", platform, buildVersion);
            try
            {
                var result = FirstLaunchVessages[key];
                return result;
            }
            catch (Exception)
            {
                try
                {
                    var configRoot = Startup.Configuration["Data:ConfigRoot"];
                    var path = string.Format("{0}/start_up_vessage_{1}.json", configRoot, key);
                    var jsonArrString = System.IO.File.ReadAllText(path);
                    var jsonArr = JArray.Parse(jsonArrString);
                    FirstLaunchVessages[key] = jsonArr;
                    return jsonArr;
                }
                catch (Exception)
                {
                    
                }
            }
            return null;
        }

        private async Task<bool> SendFirstLaunchVessage(ObjectId UserId,string platform, int buildVersion)
        {
            var jsonArr = Get1stLaunchVessages(platform, buildVersion);
            if (jsonArr == null)
            {
                return false;
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
                    }),
                    NotifyType = "NewVessageNotify",
                    ToUser = UserId.ToString()
                };
                AppServiceProvider.GetBahamutPubSubService().PublishVegeNotifyMessage(notifyMsg);
            }
            return true;
        }
    }
}
