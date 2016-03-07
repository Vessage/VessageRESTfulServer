using System;
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
                conversationId = m.ConversatinoId.ToString(),
                isRead = m.IsRead,
                sendTime = DateTimeUtil.ToAccurateDateTimeString(m.SendTime)
            };
        }

        [HttpPut("Got")]
        public void GotNewVessages()
        {

        }

        [HttpGet("Conversation/{cid}")]
        public IEnumerable<object> GetConversationVessages(string cid)
        {
            Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            return new string[] { "value1", "value2" };
        }

        [HttpPost]
        public async Task<object> SendNewVessage(string conversationId, string fileId)
        {
            var vessageService = Startup.ServicesProvider.GetVessageService();
            var vessage = new Vessage()
            {
                ConversatinoId = new ObjectId(conversationId),
                IsRead = false,
                Sender = new ObjectId(UserSessionData.UserId),
                SendTime = DateTime.UtcNow,
                Video = fileId
            };
            vessage = await vessageService.SendVessage(vessage);
            string msg = "SUCCESS";
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
            bool suc = await vessageService.SetVessageRead(vid);
            string msg = "SUCCESS";
            if (suc == false)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                msg = "SERVER_ERROR";
            }
            return new { msg = msg };
        }
    }
}
