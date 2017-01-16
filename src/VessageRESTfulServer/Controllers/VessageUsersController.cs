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
using Newtonsoft.Json.Linq;
using MongoDB.Driver.GeoJsonObjectModel;
using System.Text.RegularExpressions;

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

        [HttpGet("Profiles")]
        public async Task<IEnumerable<object>> GetUserProfiles(string userIds)
        {
            if (string.IsNullOrWhiteSpace(userIds))
            {
                Response.StatusCode = 400;
                return null;
            }
            else
            {
                var ids = from id in userIds.Split(new char[] { ',', '#', ';' }) select new ObjectId(id);
                var users = await Startup.ServicesProvider.GetUserService().GetUserProfilesByIds(ids);
                return from u in users select VessageUserToJsonObject(u);
            }
        }

        [HttpGet("MatchMobileProfiles")]
        public async Task<IEnumerable<object>> MatchUserProfilesWithMobile(string mobiles)
        {
            if (string.IsNullOrWhiteSpace(mobiles))
            {
                Response.StatusCode = 400;
                return null;
            }
            else
            {
                var mobileNos = from m in mobiles.Split(new char[] { ',', '#', ';' }) select m;
                var users = await Startup.ServicesProvider.GetUserService().MatchUsersWithMobiles(mobileNos);
                return from u in users
                       select new
                       {
                           account = u.AccountId,
                           usrId = u.Id.ToString(),
                           mobile = u.Mobile,
                           nick = u.Nick,
                           avatar = u.Avartar
                       };
            }
        }

        [HttpGet("Near")]
        public async Task<IEnumerable<object>> GetNearUsers(string location)
        {
            var enableNearUser = bool.Parse(Startup.VGConfiguration["VGConfig:nearUser:enabled"]);


            if (!enableNearUser)
            {
                Response.StatusCode = (int)HttpStatusCode.Gone;
                return new object[0];
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                var distance = int.Parse(Startup.VGConfiguration["VGConfig:nearUser:distance"]);
                var limit = int.Parse(Startup.VGConfiguration["VGConfig:nearUser:limit"]);

                var ignoreRegexPattern = Startup.VGConfiguration["VGConfig:virtualUserRegex"];
                var locationJson = JsonConvert.DeserializeObject<JObject>(location);
                var coordinates = (JArray)locationJson["coordinates"];
                var longitude = (double)coordinates.First;
                var latitude = (double)coordinates.Last;
                var geoLoc = new GeoJson2DGeographicCoordinates(longitude, latitude);
                var users = await Startup.ServicesProvider.GetUserService().GetNearUsers(UserObjectId, geoLoc, limit, distance);
                return from u in users where !Regex.IsMatch(u.AccountId, ignoreRegexPattern) select VessageUserToJsonObject(u);
            }
            else
            {
                await Startup.ServicesProvider.GetUserService().UpdateUserActiveInfo(UserObjectId, null);
                return new object[0];
            }
        }

        [HttpGet("Active")]
        public async Task<IEnumerable<object>> GetActiveUsers()
        {
            var enableActiveUser = bool.Parse(Startup.VGConfiguration["VGConfig:activeUser:enabled"]);
            var queueMaxLength = int.Parse(Startup.VGConfiguration["VGConfig:activeUser:limit"]);

            if (!enableActiveUser)
            {
                Response.StatusCode = (int)HttpStatusCode.Gone;
                return new object[0];
            }

            var users = from au in ActiveUsers where au.Id == UserObjectId select au;
            var userService = Startup.ServicesProvider.GetUserService();

            var ignoreRegexPattern = Startup.VGConfiguration["VGConfig:virtualUserRegex"];

            if (users.Count() == 0)
            {
                var user = await userService.GetUserOfUserId(UserObjectId);
                if (!string.IsNullOrEmpty(user.AccountId) && !string.IsNullOrEmpty(user.Mobile))
                {
                    if (!Regex.IsMatch(user.AccountId, ignoreRegexPattern))
                    {
                        ActiveUsers.Enqueue(user);
                    }
                    if (ActiveUsers.Count > queueMaxLength)
                    {
                        ActiveUsers.Dequeue();
                    }
                }
            }

            if (ActiveUsers.Count <= 1)
            {
                var initActiveUsers = await userService.GetActiveUsers(queueMaxLength);
                for (int i = initActiveUsers.Count() - 1; i >= 0; i--)
                {
                    var user = initActiveUsers.ElementAt(i);
                    if (!Regex.IsMatch(user.AccountId, ignoreRegexPattern))
                    {
                        ActiveUsers.Enqueue(user);
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
                motto = user.Motto,
                acTs = user.ActiveTime == null ? 0 : (long)DateTimeUtil.UnixTimeSpanOfDateTime(user.ActiveTime).TotalMilliseconds,
                location = user.Location == null ? null : new double[] { user.Location.Longitude, user.Location.Latitude }
            };

            return jsonResultObj;
        }

        [HttpPost("UserDevice")]
        public object RegistUserDevice(String deviceToken, String deviceType)
        {
            if (string.IsNullOrWhiteSpace(deviceToken) || string.IsNullOrWhiteSpace(deviceType))
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
            AppServiceProvider.GetBahamutPubSubService().PublishVegeNotifyMessage(notifyMsg);
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
            AppServiceProvider.GetBahamutPubSubService().PublishVegeNotifyMessage(notifyMsg);
            return new { msg = "OK" };
        }

        [HttpPost("SendMobileVSMS")]
        public object SendMobileVSMS(string mobile)
        {
            Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            return new { msg = "NOT_ALLOWED" };
        }

        private static async Task<int> ValidateMobSMSCode(string mobSMSAppkey, string mobile, string zone, string code)
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
        public async Task<object> NewMobileUser(string mobile, string inviteMsg = null)
        {
            var userService = this.AppServiceProvider.GetUserService();
            var user = await userService.GetUserOfMobile(mobile);
            if (user == null)
            {
                user = await userService.CreateNewUserByMobile(mobile);
                if (user != null)
                {
                    var textMsgFormat = "{\"textMessage\":\"{0}\"}";
                    var inviteVessage = new Vessage
                    {
                        Id = ObjectId.GenerateNewId(),
                        IsGroup = false,
                        IsRead = false,
                        Sender = UserObjectId,
                        SendTime = DateTime.UtcNow,
                        TypeId = Vessage.TYPE_FACE_TEXT,
                        Ready = true,
                        Body = string.Format(textMsgFormat, string.IsNullOrWhiteSpace(inviteMsg) ? Startup.VGConfiguration["defaultInviteMessage"] : inviteMsg)
                    };
                    await this.AppServiceProvider.GetVessageService().SendVessagesToUser(user.Id, new Vessage[] { inviteVessage });
                    var sender = await userService.GetUserOfUserId(UserObjectId);
                    await NotifyAdminHelper.NotifyAdminUserInviteNewMobileAccount(sender, mobile, userService);
                }
            }
            if (user == null)
            {
                Response.StatusCode = 500;
            }
            return VessageUserToJsonObject(user);
        }

        [HttpPost("ValidateMobileVSMS")]
        public async Task<object> ValidateMobileVSMS(string smsAppkey, string mobile, string zone, string code, bool bindExistsAccount = true)
        {

            var sessionData = UserSessionData;
            var userId = sessionData.UserId;
            var userOId = UserObjectId;
            var userService = Startup.ServicesProvider.GetUserService();
            var profile = await userService.GetUserOfUserId(userOId);
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
                if (profile.Mobile == null && bindExistsAccount)
                {
                    registedUser = await userService.BindExistsUserOnRegist(userOId, mobile);
                }
                if (registedUser == null)
                {
                    bool suc = await userService.UpdateMobileOfUser(userOId, mobile);
                    if (suc)
                    {
                        UpdateBahamutAccountMobile(sessionData, mobile);
                        if (bindExistsAccount == false)
                        {
                            await userService.RemoveExistsNullAccountUserOfMobileAsync(mobile, profile.AccountId);
                        }
                        return new { msg = "SUCCESS" };
                    }
                }
                else
                {
                    var tokenService = Startup.ServicesProvider.GetTokenService();
                    sessionData.UserId = registedUser.Id.ToString();
                    if (await tokenService.SetUserSessionDataAsync(sessionData))
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
        public async Task<object> UpdateChatImage(string image, string imageType)
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
