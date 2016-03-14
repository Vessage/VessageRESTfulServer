﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using VessageRESTfulServer.Services;
using VessageRESTfulServer.Models;
using BahamutService.Service;
using MongoDB.Bson;
using System.Net;
using BahamutCommon;

namespace VessageRESTfulServer.Controllers
{
    [Route("api/[controller]")]
    public class VessagesController : APIControllerBase
    {

        [HttpGet("New")]
        public async Task<IEnumerable<object>> GetNewVessages()
        {
            IEnumerable<Vessage> msgs = await Startup.ServicesProvider.GetVessageService().GetNotReadMessageOfUser(UserSessionData.UserId);
            IEnumerable<object> result = from m in msgs select GenerateVessageJsonObject(m);
            return result;
        }

        private object GenerateVessageJsonObject(Vessage m)
        {

            return new
            {
                vessageId = m.Id.ToString(),
                fileId = m.Video,
                sender = m.Sender.ToString(),
                isRead = m.IsRead,
                sendTime = DateTimeUtil.ToAccurateDateTimeString(m.SendTime)
            };
        }

        [HttpPut("Got")]
        public async Task GotNewVessages()
        {
            await AppServiceProvider.GetVessageService().UpdateGodMessageTime(UserSessionData.UserId);
        }

        [HttpPost]
        public async Task<object> SendNewVessage(string receiverId,string receiverMobile, string fileId)
        {
            var vessage = new Vessage()
            {
                Id = ObjectId.GenerateNewId(),
                IsRead = false,
                Sender = new ObjectId(UserSessionData.UserId),
                SendTime = DateTime.UtcNow,
                Video = fileId
            };

            var vessageService = Startup.ServicesProvider.GetVessageService();
            if (string.IsNullOrWhiteSpace(receiverId) == false)
            {
                await vessageService.SendVessage(new ObjectId(receiverId), vessage);
            }else if (string.IsNullOrWhiteSpace(receiverMobile) == false)
            {
                var result = await vessageService.SendVessageForMobile(receiverMobile, vessage);
                if (result.Item2 != ObjectId.Empty)
                {
                    return new { msg = "SUCCESS", chatterId = result.Item2.ToString() };
                }
                else
                {
                    return new { msg = "SUCCESS" };
                }
            }
            string msg = "FAIL";
            if (vessage == null)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                msg = "SERVER_ERROR";
            }
            return new { msg = msg };
        }

        [HttpPut("Read/{vid}")]
        public async Task<object> ReadVessage(string vid)
        {
            var vessageService = Startup.ServicesProvider.GetVessageService();
            bool suc = await vessageService.SetVessageRead(UserSessionData.UserId, vid);
            string msg = "SUCCESS";
            if (suc == false)
            {
                Response.StatusCode = (int)HttpStatusCode.Conflict;
                msg = "FAIL";
            }
            return new { msg = msg };
        }
    }
}