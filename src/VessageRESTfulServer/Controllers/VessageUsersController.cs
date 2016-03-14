using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using VessageRESTfulServer.Services;
using System.Net;
using VessageRESTfulServer.Models;
using MongoDB.Bson;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{
    [Route("api/[controller]")]
    public class VessageUsersController : APIControllerBase
    {
        // GET: api/values
        [HttpGet]
        public async Task<object> Get()
        {
            var userService = Startup.ServicesProvider.GetUserService();
            var user = await userService.GetUserOfUserId(UserSessionData.UserId);
            return VessageUserToJsonObject(user);
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
                return new { msg = "NO_SUCH_USER" };
            }
        }

        [HttpGet("Mobile/{mobile}")]
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
                return new { msg = "NO_SUCH_USER" };
            }
        }

        private object VessageUserToJsonObject(VessageUser user)
        {
            var jsonResultObj = new
            {
                accountId = user.AccountId,
                userId = user.Id.ToString(),
                mainChatImage = user.MainChatImage,
                avatar = user.Avartar,
                mobile = user.Mobile
            };
            return jsonResultObj;
        }

        [HttpPost("SendMobileVSMS")]
        public async Task<object> SendMobileVSMS(string mobile)
        {
            return new { msg = "SUCCESS" };
        }

        [HttpPost("ValidateMobileVSMS")]
        public async Task<object> ValidateMobileVSMS(string mobile, string vsms)
        {
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
