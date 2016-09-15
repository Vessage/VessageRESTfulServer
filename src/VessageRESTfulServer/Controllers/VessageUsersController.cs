using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VessageRESTfulServer.Services;
using VessageRESTfulServer.Models;
using MongoDB.Bson;
using BahamutCommon;
using System.Text;
using System.IO;
using BahamutService.Service;
using Newtonsoft.Json;
using BahamutService.Model;
using BahamutService;
using System.Net;
using System.Net.Http;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{
    [Route("api/[controller]")]
    public class VessageUsersController : APIControllerBase
    {
        private static Queue<VessageUser> ActiveUsers = new Queue<VessageUser>();
        
        [HttpGet]
        public async Task<object> Get()
        {
            var userService = Startup.ServicesProvider.GetUserService();
            var user = await userService.GetUserOfUserId(UserObjectId);
            return VessageUserToJsonObject(user);
        }

        [HttpGet("Near")]
        public Task<IEnumerable<object>> GetNearUsers(string location)
        {
            Response.StatusCode = (int)HttpStatusCode.Gone;
            return null;
        }

        [HttpGet("Active")]
        public async Task<IEnumerable<object>> GetActiveUsers()
        {
            if (Startup.IsProduction)
            {
                var vegeActiveUserLock = "/etc/bahamut/vege/active_user.lock";
                if (System.IO.File.Exists(vegeActiveUserLock))
                {
                    Response.StatusCode = (int)HttpStatusCode.Gone;
                    return null;
                }
            }
            var users = from au in ActiveUsers where au.Id == UserObjectId select au;
            if (users.Count() == 0)
            {
                var userService = Startup.ServicesProvider.GetUserService();
                var user = await userService.GetUserOfUserId(UserObjectId);
                if (!string.IsNullOrEmpty(user.AccountId) && !string.IsNullOrEmpty(user.Mobile))
                {                    
                    ActiveUsers.Enqueue(user);
                    if (ActiveUsers.Count > 20)
                    {
                        ActiveUsers.Dequeue();
                    }
                }
            }
            var result = new List<object>();
            foreach (var u in ActiveUsers)
            {
                if (u.Id != UserObjectId)
                {
                    result.Add(VessageUserToJsonObject(u));
                }
            }
            return result;
        }

        [HttpGet("UserId/{userId}")]
        public async Task<object> Get(string userId)
        {
            var userService = Startup.ServicesProvider.GetUserService();
            var user = await userService.GetUserOfUserId(new ObjectId(userId));
            return VessageUserToJsonObject(user);
        }

        [HttpGet("AccountId/{accountId}")]
        public async Task<object> GetUserByAccountId(string accountId)
        {
            var userService = Startup.ServicesProvider.GetUserService();
            VessageUser user = await userService.GetUserOfAccountId(accountId);
            if (user != null)
            {
                return VessageUserToJsonObject(user);
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return null;
            }
        }

        [HttpGet("Mobile")]
        public async Task<object> GetUserByMobile(string mobile)
        {
            var userService = Startup.ServicesProvider.GetUserService();
            VessageUser user = await userService.GetUserOfMobile(mobile);
            if (user != null)
            {
                return VessageUserToJsonObject(user);
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return null;
            }
        }

        private object VessageUserToJsonObject(VessageUser user)
        {
            if (user == null)
            {
                return null;
            }
            var jsonResultObj = new
            {
                accountId = user.AccountId,
                userId = user.Id.ToString(),
                mainChatImage = user.MainChatImage,
                avatar = user.Avartar,
                nickName = user.Nick,
                mobile = UserObjectId == user.Id ? user.Mobile : StringUtil.Md5String(user.Mobile),
                sex = user.Sex,
                motto = user.Motto
            };

            return jsonResultObj;
        }

        [HttpPost("UserDevice")]
        public object RegistUserDevice(String deviceToken,String deviceType)
        {
            if(string.IsNullOrWhiteSpace(deviceToken) || string.IsNullOrWhiteSpace(deviceType))
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return new { msg = "PARAMETERS_ERROR" };
            }
            var notifyMsg = new BahamutPublishModel
            {
                NotifyType = "RegistUserDevice",
                Info = JsonConvert.SerializeObject(new
                {
                    AccountId = UserSessionData.AccountId,
                    Appkey = UserSessionData.Appkey,
                    DeviceToken = deviceToken,
                    DeviceType = deviceType
                }, Formatting.None),
                ToUser = UserSessionData.UserId
            };
            AppServiceProvider.GetBahamutPubSubService().PublishBahamutUserNotifyMessage("Vege", notifyMsg);
            return new { msg = "OK" };
        }

        [HttpDelete("UserDevice")]
        public object RemoveUserDevice(String deviceToken)
        {
            var notifyMsg = new BahamutPublishModel
            {
                NotifyType = "RemoveUserDevice",
                Info = JsonConvert.SerializeObject(new
                {
                    AccountId = UserSessionData.AccountId,
                    Appkey = UserSessionData.Appkey,
                    DeviceToken = deviceToken
                }, Formatting.None),
                ToUser = UserSessionData.UserId
            };
            AppServiceProvider.GetBahamutPubSubService().PublishBahamutUserNotifyMessage("Vege", notifyMsg);
            return new { msg = "OK" };
        }

        [HttpPost("SendMobileVSMS")]
        public object SendMobileVSMS(string mobile)
        {
            Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            return new { msg = "NOT_ALLOWED" };
        }

        private static async Task<int> ValidateMobSMSCode(string mobSMSAppkey,string mobile, string zone, string code)
        {
#if DEBUG
            if (1 == int.Parse("1"))
            {
                return 200;
            }
#endif
            return await Task.Run(async () =>
             {
                 WebRequest request = WebRequest.Create("https://webapi.sms.mob.com/sms/verify");
                 request.Proxy = null;
                 request.Credentials = CredentialCache.DefaultCredentials;

                 var data = string.Format("appkey={0}&amp;phone={1}&amp;zone={2}&amp;code={3}", mobSMSAppkey, mobile, zone, code);
                 byte[] bs = Encoding.UTF8.GetBytes(data);
                 request.Method = "Post";
                 using (Stream reqStream = await request.GetRequestStreamAsync())
                 {
                     
                     reqStream.Write(bs, 0, bs.Length);
                 }
                 var response = await request.GetResponseAsync();
                 Stream dataStream = response.GetResponseStream();
                 StreamReader reader = new StreamReader(dataStream);
                 string responseFromServer = reader.ReadToEnd();
                 dynamic responseObj = JsonConvert.DeserializeObject(responseFromServer);
                 try
                 {
                     return responseObj.status;
                 }
                 catch (Exception)
                 {
                     return -1;
                 }
             });
        }

        [HttpPost("NewMobileUser")]
        public async Task<object> NewMobileUser(string mobile)
        {
            var userService = this.AppServiceProvider.GetUserService();
            var user = await userService.GetUserOfMobile(mobile);
            if (user == null)
            {
                user = await userService.CreateNewUserByMobile(mobile);
            }
            if (user == null)
            {
                Response.StatusCode = 500;
            }
            return VessageUserToJsonObject(user); 
        }

        [HttpPost("ValidateMobileVSMS")]
        public async Task<object> ValidateMobileVSMS(string smsAppkey,string mobile, string zone, string code)
        {
            
            var sessionData = UserSessionData;
            var userId = sessionData.UserId;
            var userOId = UserObjectId;
            var userService = Startup.ServicesProvider.GetUserService();
            var profile = await userService.GetUserOfUserId(UserObjectId);
            if (!string.IsNullOrWhiteSpace(profile.Mobile) && profile.Mobile == mobile)
            {
                return new { msg = "SUCCESS" };
            }
            var mobSMSAppkey = string.IsNullOrEmpty(smsAppkey) ? Startup.Configuration["Data:MobSMSAppKey"] : smsAppkey;
            var res = await ValidateMobSMSCode(mobSMSAppkey, mobile, zone, code);
            if (res != 200)
            {
                Response.StatusCode = res;
                return new { msg = res.ToString() };
            }

            try
            {
                VessageUser registedUser = null;
                if (profile.Mobile == null)
                {
                    registedUser = await userService.BindExistsUserOnRegist(userOId, mobile);
                }
                if (registedUser == null)
                {
                    bool suc = await userService.UpdateMobileOfUser(userOId, mobile);
                    if (suc)
                    {
                        UpdateBahamutAccountMobile(sessionData, mobile);
                        return new { msg = "SUCCESS" };
                    }
                }
                else
                {
                    var tokenService = Startup.ServicesProvider.GetTokenService();
                    sessionData.UserId = registedUser.Id.ToString();
                    if(await tokenService.SetUserSessionDataAsync(sessionData))
                    {
                        await tokenService.ReleaseAppTokenAsync(sessionData.Appkey, userId, sessionData.AppToken);
                        UpdateBahamutAccountMobile(sessionData, mobile);
                        return new
                        {
                            msg = "SUCCESS",
                            newUserId = sessionData.UserId
                        };
                    }
                    else
                    {
                        throw new Exception("Alloc User Session Error");
                    }
                    
                }
            }
            catch (Exception ex)
            {
                LogWarning(ex.Message);
            }

            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return new { msg = "SERVER_ERROR" };
        }

        private async void UpdateBahamutAccountMobile(AccountSessionData sessionData, string newMobile)
        {
            HttpClient client = new HttpClient();
            string url = string.Format("{0}/BahamutAccounts/AccountMobile", Startup.AuthServerUrl);
            var kvList = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("appkey", Startup.Appkey),
                new KeyValuePair<string, string>("appToken", UserSessionData.AppToken),
                new KeyValuePair<string, string>("accountId", UserSessionData.AccountId),
                new KeyValuePair<string, string>("userId", sessionData.UserId),
                new KeyValuePair<string, string>("newMobile", newMobile)
            };
            await client.PutAsync(url, new FormUrlEncodedContent(kvList));
        }

        [HttpPut("Nick")]
        public async Task<object> ChangeNick(string nick)
        {
            if (string.IsNullOrWhiteSpace(nick) == false)
            {
                var userService = Startup.ServicesProvider.GetUserService();
                bool suc = await userService.ChangeNickOfUser(UserObjectId, nick);
                if (suc)
                {
                    return new { msg = "SUCCESS" };
                }
                else
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return new { msg = "SERVER_ERROR" };
                }
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return new { msg = "INVALID_VALUE" };
            }
        }

        [HttpPut("Motto")]
        public async Task<object> ChangeMotto(string motto)
        {
            if (string.IsNullOrWhiteSpace(motto) == false)
            {
                var userService = Startup.ServicesProvider.GetUserService();
                bool suc = await userService.ChangeMottoOfUser(UserObjectId, motto);
                if (suc)
                {
                    return new { msg = "SUCCESS" };
                }
                else
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return new { msg = "SERVER_ERROR" };
                }
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return new { msg = "INVALID_VALUE" };
            }
        }

        [HttpPut("SexValue")]
        public async Task<object> ChangeSexValue(int value)
        {
            var userService = Startup.ServicesProvider.GetUserService();
            bool suc = await userService.ChangeSexValue(UserObjectId, value);
            if (suc)
            {
                return new { msg = "SUCCESS" };
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return new { msg = "SERVER_ERROR" };
            }
        }

        [HttpPut("Avatar")]
        public async Task<object> ChangeAvatar(string avatar)
        {
            if (string.IsNullOrWhiteSpace(avatar) == false)
            {
                var userService = Startup.ServicesProvider.GetUserService();
                bool suc = await userService.ChangeAvatarOfUser(UserObjectId, avatar);
                if (suc)
                {
                    return new { msg = "SUCCESS" };
                }
                else
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return new { msg = "SERVER_ERROR" };
                }
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return new { msg = "INVALID_VALUE" };
            }
        }

        [HttpGet("ChatImages")]
        public async Task<object> GetChatImages(string userId)
        {
            var images = await AppServiceProvider.GetUserService().GetUserChatImages(new ObjectId(userId));
            return new
            {
                userId = userId,
                chatImages = from i in images
                             select new
                             {
                                 imageId = i.ImageFileId,
                                 imageType = i.ImageType
                             }
            };
        }

        [HttpPut("ChatImages")]
        public async Task<object> UpdateChatImage(string image,string imageType)
        {
            if (string.IsNullOrWhiteSpace(image) == false)
            {
                var userService = Startup.ServicesProvider.GetUserService();
                bool suc = await userService.UpdateChatImageOfUser(UserObjectId, image, imageType);
                if (suc)
                {
                    return new { msg = "SUCCESS" };
                }
                else
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return new { msg = "SERVER_ERROR" };
                }
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return new { msg = "INVALID_VALUE" };
            }
        }

        [HttpPut("MainChatImage")]
        public async Task<object> ChangeMainChatImage(string image)
        {
            if (string.IsNullOrWhiteSpace(image) == false)
            {
                var userService = Startup.ServicesProvider.GetUserService();
                bool suc = await userService.ChangeMainChatImageOfUser(UserObjectId, image);
                if (suc)
                {
                    return new { msg = "SUCCESS" };
                }
                else
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return new { msg = "SERVER_ERROR" };
                }
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return new { msg = "INVALID_VALUE" };
            }
        }

    }
}
