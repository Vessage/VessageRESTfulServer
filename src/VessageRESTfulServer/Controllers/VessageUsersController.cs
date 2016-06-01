using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using VessageRESTfulServer.Services;
using System.Net;
using VessageRESTfulServer.Models;
using MongoDB.Bson;
using BahamutCommon;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.IO;
using System.Net.Security;
using BahamutService.Service;

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
            var user = await userService.GetUserOfUserId(UserSessionData.UserId);
            return VessageUserToJsonObject(user);
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
                    if (Response.StatusCode == (int)HttpStatusCode.Gone)
                    {
                        return null;
                    }
                }
            }
            var users = from au in ActiveUsers where au.Id == UserObjectId select au;
            if (users.Count() == 0)
            {
                var userService = Startup.ServicesProvider.GetUserService();
                var user = await userService.GetUserOfUserId(UserSessionData.UserId);
                ActiveUsers.Enqueue(user);
                if (ActiveUsers.Count > 20)
                {
                    ActiveUsers.Dequeue();
                }
            }
            var result = from u in ActiveUsers
                         where u.Id != UserObjectId
                         select new
                         {
                             accountId = u.AccountId,
                             userId = u.Id.ToString(),
                             mainChatImage = u.MainChatImage,
                             avatar = u.Avartar,
                             nickName = u.Nick
                         };
            return result;
        }

        [HttpGet("UserId/{userId}")]
        public async Task<object> Get(string userId)
        {
            var userService = Startup.ServicesProvider.GetUserService();
            var user = await userService.GetUserOfUserId(userId);
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
			if(user == null)
			{
				return null;
			}
            var mobile = UserSessionData.UserId == user.Id.ToString() ? user.Mobile : StringUtil.Md5String(user.Mobile);
            if(mobile == null)
            {
                mobile = "";
            }
            var jsonResultObj = new
            {
                accountId = user.AccountId,
                userId = user.Id.ToString(),
                mainChatImage = user.MainChatImage,
                avatar = user.Avartar,
				nickName = user.Nick,
                mobile = mobile
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
                Info = Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    AccountId = UserSessionData.AccountId,
                    Appkey = UserSessionData.Appkey,
                    DeviceToken = deviceToken,
                    DeviceType = deviceType
                }),
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
                Info = Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    AccountId = UserSessionData.AccountId,
                    Appkey = UserSessionData.Appkey,
                    DeviceToken = deviceToken
                }),
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
            return await Task.Run(() =>
             {
                 WebRequest request = WebRequest.Create("https://webapi.sms.mob.com/sms/verify");
                 request.Proxy = null;
                 request.Credentials = CredentialCache.DefaultCredentials;

                //allows for validation of SSL certificates 

                ServicePointManager.ServerCertificateValidationCallback += new System.Net.Security.RemoteCertificateValidationCallback(ValidateServerCertificate);
                 var data = string.Format("appkey={0}&amp;phone={1}&amp;zone={2}&amp;code={3}", mobSMSAppkey, mobile, zone, code);
                 byte[] bs = Encoding.UTF8.GetBytes(data);
                 request.Method = "Post";
                 using (Stream reqStream = request.GetRequestStream())
                 {
                     reqStream.Write(bs, 0, bs.Length);
                 }
                 HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                 Stream dataStream = response.GetResponseStream();
                 StreamReader reader = new StreamReader(dataStream);
                 string responseFromServer = reader.ReadToEnd();
                 dynamic responseObj = Newtonsoft.Json.JsonConvert.DeserializeObject(responseFromServer);
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

        //for testing purpose only, accept any dodgy certificate... 
        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        [HttpPost("ValidateMobileVSMS")]
        public async Task<object> ValidateMobileVSMS(string smsAppkey,string mobile, string zone, string code)
        {
            var mobSMSAppkey = smsAppkey == null ? Startup.Configuration["Data:MobSMSAppKey"] : smsAppkey;
            var res = await ValidateMobSMSCode(mobSMSAppkey, mobile, zone, code);
            if (res != 200)
            {
                Response.StatusCode = res;
                return new { msg = res.ToString() };
            }
            var userId = UserSessionData.UserId;
            var userOId = new ObjectId(userId);
            var userService = Startup.ServicesProvider.GetUserService();
            try
            {
                bool suc = await userService.UpdateMobileOfUser(userId, mobile);
                if (suc)
                {
                    var vb = await AppServiceProvider.GetVessageService().BindNewUserReveicedVessages(userId, mobile);
                    return new { msg = "SUCCESS" };
                }
            }
            catch (Exception ex)
            {
                LogWarning(ex.Message);
            }

            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return new { msg = "SERVER_ERROR" };
        }

        [HttpPut("Nick")]
        public async Task<object> ChangeNick(string nick)
        {
            if (string.IsNullOrWhiteSpace(nick) == false)
            {
                var userService = Startup.ServicesProvider.GetUserService();
                bool suc = await userService.ChangeNickOfUser(UserSessionData.UserId, nick);
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

        [HttpPut("Avatar")]
        public async Task<object> ChangeAvatar(string avatar)
        {
            if (string.IsNullOrWhiteSpace(avatar) == false)
            {
                var userService = Startup.ServicesProvider.GetUserService();
                bool suc = await userService.ChangeAvatarOfUser(UserSessionData.UserId, avatar);
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
                bool suc = await userService.ChangeMainChatImageOfUser(UserSessionData.UserId, image);
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
