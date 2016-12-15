using System;
using Microsoft.AspNetCore.Mvc;
using BahamutService.Model;
using NLog;
using MongoDB.Bson;

namespace VessageRESTfulServer.Controllers
{
    public class APIControllerBase : Controller, IGetAccountSessionData
    {
        public APIControllerBase()
        {
        }

        public IServiceProvider AppServiceProvider { get { return Startup.ServicesProvider; } }

        public AccountSessionData UserSessionData
        {
            get
            {
                return Request.HttpContext.Items["AccountSessionData"] as AccountSessionData;
            }
        }

        public ObjectId UserObjectId
        {
            get
            {
                return new ObjectId(UserSessionData.UserId);
            }
        }

        public void LogInfo(string message,params object[] args)
        {
            LogManager.GetLogger("Info").Info(message, args);
        }

        public void LogWarning(string message,Exception exception = null)
        {
            if(exception == null)
            {
                LogManager.GetLogger("Warning").Warn(message);
            }
            else
            {
                LogManager.GetLogger("Warning").Warn(exception, message);
            }
        }
    }
}
