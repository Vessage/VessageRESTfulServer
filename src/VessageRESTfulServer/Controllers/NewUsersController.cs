using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using VessageRESTfulServer.Services;
using VessageRESTfulServer.Models;
using BahamutService;
using BahamutService.Service;
using Newtonsoft.Json;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{
    [Route("[controller]")]
    public class NewUsersController : Controller
    {

        // POST api/values
        [HttpPost]
        public async Task<object> Post(string accountId, string accessToken, string nickName, string motto, string mobile = null, string region = "cn")
        {
            var userService = Startup.ServicesProvider.GetUserService();
            var test = await userService.GetUserOfAccountId(accountId);
            if (test != null)
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return "One AccountId Only Regist One Time";
            }

            var tokenService = Startup.ServicesProvider.GetTokenService();
            var userSession = await tokenService.ValidateToGetSessionDataAsync(Startup.Appkey, accountId, accessToken);
            if (userSession != null)
            {
                var newUser = new VessageUser()
                {
                    AccountId = accountId,
                    CreateTime = DateTime.UtcNow,
                    Nick = nickName,
                    Sex = 0,
                    Mobile = mobile
                };

                newUser = await userService.CreateNewUser(newUser);
                var userId = newUser.Id.ToString();
                var sessionData = await tokenService.ValidateAccessTokenAsync(Startup.Appkey, accountId, accessToken, userId);
                await NotifyAdminAsync(accountId, userService);
                return new
                {
                    succeed = true,
                    appToken = sessionData.UserSessionData.AppToken,
                    userId = sessionData.UserSessionData.UserId,
                    apiServer = Startup.ServiceApiUrlRoute,
                    fileAPIServer = Startup.FileApiUrl,
                    chicagoServer = string.Format("{0}:{1}", Startup.ChicagoServerAddress, Startup.ChicagoServerPort)
                };
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return "Validate Fail,Can't Not Regist New User";
            }

        }

        private static IDictionary<string, string> adminUserId = new Dictionary<string, string>();

        private async Task NotifyAdminAsync(string newAccountId, UserService userService)
        {
            try
            {
                var enableNotify = bool.Parse(Startup.VGConfiguration["VGConfig:UserRegistedNotifyAdmin:enable"]);
                if (enableNotify)
                {
                    var admins = Startup.VGConfiguration.GetSection("VGConfig:UserRegistedNotifyAdmin:Admins").GetChildren();
                    var notifyAdmins = new List<string>();
                    foreach (var admin in admins)
                    {
                        var adminAccountId = admin.Value;
                        var userId = "";
                        try
                        {
                            userId = adminUserId[adminAccountId];
                        }
                        catch (System.Exception)
                        {
                            var adminUser = await userService.GetUserOfAccountId(adminAccountId);
                            if (adminUser != null)
                            {
                                userId = adminUser.Id.ToString();
                                adminUserId.Add(adminAccountId, userId);
                            }
                        }
                        if (string.IsNullOrEmpty(userId) == false)
                        {
                            notifyAdmins.Add(userId);
                        }
                    }

                    PostBahamutNotification(notifyAdmins, newAccountId);
                }
            }
            catch (System.Exception)
            {
            }
        }

        private void PostBahamutNotification(IEnumerable<string> notifyAdmins, string registedAccountId)
        {
            if (notifyAdmins.Count() == 0)
            {
                return;
            }
            var notifyMsg = new BahamutPublishModel
            {
                NotifyInfo = JsonConvert.SerializeObject(new
                {
                    BuilderId = 2,
                    AfterOpen = "go_custom",
                    Custom = "ActivityUpdatedNotify",
                    Text = notifyAdmins.First(),
                    LocKey = string.Format("新用户注册:{0}", registedAccountId)
                }, Formatting.None),
                NotifyType = "ActivityUpdatedNotify",
                ToUser = notifyAdmins.Count() > 1 ? string.Join(",", notifyAdmins) : notifyAdmins.First()
            };
            Startup.ServicesProvider.GetBahamutPubSubService().PublishVegeNotifyMessage(notifyMsg);
        }
    }

}
