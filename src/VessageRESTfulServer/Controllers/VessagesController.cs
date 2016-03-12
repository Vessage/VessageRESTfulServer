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
        public async void GotNewVessages()
        {
            await AppServiceProvider.GetVessageService().UpdateGodMessageTime(UserSessionData.UserId);
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
            var vessage = new Vessage()
            {
                IsRead = false,
                Sender = new ObjectId(UserSessionData.UserId),
                SendTime = DateTime.UtcNow,
                Video = fileId
            };

            var vessageService = Startup.ServicesProvider.GetVessageService();
            var conversationService = Startup.ServicesProvider.GetConversationService();
            Conversation senderConversation = await conversationService.GetConversationOfUser(UserSessionData.UserId, conversationId);
            Conversation reveicerConversation = null;
            if (senderConversation.ChattingUserId != null)
            {
                reveicerConversation = await conversationService.GetConversationOfReceiverId(senderConversation.ChattingUserId, UserSessionData.UserId);
                vessage.ConversatinoId = reveicerConversation.Id;
                vessage = await vessageService.SendVessage(reveicerConversation.UserId, vessage);
                AppServiceProvider.GetBahamutPubSubService().PublishBahamutUserNotifyMessage("VessageRESTfulServer", reveicerConversation.UserId.ToString(), new BahamutUserAppNotifyMessage()
                {
                     NotificationType = "NewVessageNotify"
                });
            }
            else if(senderConversation.ChattingUserMobile != null)
            {
                var userService = AppServiceProvider.GetUserService();
                var sender = await userService.GetUserOfUserId(UserSessionData.UserId);
                reveicerConversation = await conversationService.GetConversationOfReceiverMobile(senderConversation.ChattingUserMobile, UserSessionData.UserId, sender.Nick);
                vessage.ConversatinoId = reveicerConversation.Id;
                vessage = await vessageService.SendVessageForMobile(senderConversation.ChattingUserMobile, vessage);
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return new { msg = "" };
            }
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
            bool suc = await vessageService.SetVessageRead(UserSessionData.UserId, vid);
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
