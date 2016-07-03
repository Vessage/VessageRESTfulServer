using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using BahamutService;
using NLog;
using ServerControlService.Service;
using VessageRESTfulServer.Services;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{

    [Route("[controller]")]
    public class TokensController : APIControllerBase
    {
        [HttpGet]
        public async Task<object> Get(string appkey, string accountId, string accessToken)
        {
            LogInfo("Account:{0} Validating", accountId);
            var tokenService = Startup.ServicesProvider.GetTokenService();
            var userService = Startup.ServicesProvider.GetService<UserService>();
            try
            {
                var user = await userService.GetUserOfAccountId(accountId);
                string userId = user == null ? null : user.Id.ToString();
                if (userId == null)
                {
                    var tr = await tokenService.ValidateToGetSessionData(appkey, accountId, accessToken);
                    if (tr != null)
                    {
                        LogInfo("Account:{0} Registing", accountId);
                        return new
                        {
                            RegistAPIServer = Startup.Server
                        };
                    }
                    LogManager.GetLogger("Warning").Warn("Validate Failed:Account:{0} Token:{1} Appkey:{2}", appkey, accountId, accessToken);
                    Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return "Validate Failed";
                }
                var tokenResult = await tokenService.ValidateAccessToken(appkey, accountId, accessToken, userId);
                if (tokenResult.Succeed)
                {
                    LogInfo("User:{0} Validate Success", userId);
                    Startup.ValidatedUsers[userId] = tokenResult.UserSessionData.AppToken;
                    return new
                    {
                        AppToken = tokenResult.UserSessionData.AppToken,
                        UserId = userId,
                        APIServer = Startup.APIUrl,
                        FileAPIServer = Startup.FileApiUrl,
                        ChicagoServer = string.Format("{0}:{1}", Startup.ChicagoServerAddress, Startup.ChicagoServerPort)
                    };
                }
                else
                {
                    Startup.ValidatedUsers.Remove(userId);
                    Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return tokenResult.Message;
                }
            }
            catch (Exception ex)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return new
                {
                    msg = ex.Message
                };
            }

        }

        // DELETE api/values/5
        [HttpDelete]
        public async Task<bool> Delete(string appkey, string userId, string appToken)
        {
            return await Task.Run(() =>
            {
                var tokenService = Startup.ServicesProvider.GetTokenService();
                Startup.ValidatedUsers.Remove(userId);
                return tokenService.ReleaseAppToken(appkey, userId, appToken);
            });
        }
    }
}
