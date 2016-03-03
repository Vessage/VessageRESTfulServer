using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using BahamutCommon;
using BahamutService.Model;
using MongoDB.Driver;
using NLog;

namespace VessageRESTfulServer.Controllers
{
    public class APIControllerBase : Controller, IGetAccountSessionData
    {
        public APIControllerBase()
        {
        }

        public AccountSessionData UserSessionData
        {
            get { return Request.HttpContext.Items["AccountSessionData"] as AccountSessionData; }
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
