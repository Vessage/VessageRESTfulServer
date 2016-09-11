using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using VessageRESTfulServer.Services;
using VessageRESTfulServer.Models;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{
    [Route("[controller]")]
    public class NewUsersController : Controller
    {

        // POST api/values
        [HttpPost]
        public async Task<object> Post(string accountId, string accessToken, string nickName, string motto,string mobile = null, string region = "cn")
        {
            var userService = Startup.ServicesProvider.GetUserService();
            var test = await userService.GetUserOfAccountId(accountId);
            if (test != null)
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return "One AccountId Only Regist One Time";
            }

            var tokenService = Startup.ServicesProvider.GetTokenService();
            var userSession = await tokenService.ValidateToGetSessionData(Startup.Appkey, accountId, accessToken);
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
                var sessionData = await tokenService.ValidateAccessToken(Startup.Appkey, accountId, accessToken, userId);
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
    }
}
