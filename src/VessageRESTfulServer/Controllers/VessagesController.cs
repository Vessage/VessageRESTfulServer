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
                isRead = m.IsRead,
                extraInfo = m.ExtraInfo,
                sendTime = DateTimeUtil.ToAccurateDateTimeString(m.SendTime)
            };
        }

        [HttpPut("Got")]
        public async Task GotNewVessages()
        {
            await AppServiceProvider.GetVessageService().UpdateGodMessageTime(UserSessionData.UserId);
        }

        [HttpPut("FinishSendVessage")]
        public async Task<object> FinishSendVessage(string vessageBoxId,string vessageId,string fileId)
        {
            var result = await AppServiceProvider.GetVessageService().FinishSendVessage(vessageBoxId, UserSessionData.UserId, vessageId, fileId);
            var msg = "SUCCESS";
            if (result.Item1 != ObjectId.Empty)
            {
                var notifyMsg = new BahamutPublishModel
                {
                    NotifyType = "NewVessageNotify",
                    ToUser = result.Item1.ToString()
                };
                AppServiceProvider.GetBahamutPubSubService().PublishBahamutUserNotifyMessage("Vege", notifyMsg);
            }
            else if (!string.IsNullOrWhiteSpace(result.Item2))
            {
                //TODO: Send sms to the mobile user
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.NotModified;
            }
            return new { msg = msg };
        }

        [HttpPut("CancelSendVessage")]
        public async Task<object> CancelSendVessage(string vessageBoxId,string vessageId)
        {
            bool suc = await AppServiceProvider.GetVessageService().CancelSendVessage(vessageId, UserSessionData.UserId, vessageId);

            var msg = "SUCCESS";
            if (suc == false)
            {
                Response.StatusCode = (int)HttpStatusCode.NotModified;
                msg = "FAIL";
            }
            return new { msg = msg };
        }

        [HttpPost("ForMobile")]
        public async Task<object> SendNewVessageForMobile(string receiverMobile, string extraInfo)
        {
            Vessage vessage = null;
            Tuple<ObjectId, ObjectId> result = null;
            var vessageService = Startup.ServicesProvider.GetVessageService();
            if (string.IsNullOrWhiteSpace(receiverMobile) == false)
            {
                vessage = new Vessage()
                {
                    Id = ObjectId.GenerateNewId(),
                    IsRead = false,
                    Sender = new ObjectId(UserSessionData.UserId),
                    SendTime = DateTime.UtcNow,
                    VideoReady = false,
                    ExtraInfo = extraInfo
                };
                var receiver = await AppServiceProvider.GetUserService().GetUserOfMobile(receiverMobile);
                if (receiver == null)
                {
                    result = await vessageService.SendVessageForMobile(receiverMobile, vessage);
                }
                else
                {
                    result = await vessageService.SendVessage(receiver.Id, vessage);
                }
            }

            if (result == null)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return new { msg = "FAIL" };
            }

            return new
            {
                vessageBoxId = result.Item1.ToString(),
                vessageId = result.Item2.ToString()
            };
        }

        [HttpPost("ForUser")]
        public async Task<object> SendNewVessageForUser(string receiverId, string extraInfo)
        {
            Vessage vessage = null;
            Tuple<ObjectId, ObjectId> result = null;
            var vessageService = Startup.ServicesProvider.GetVessageService();
            if (string.IsNullOrWhiteSpace(receiverId) == false)
            {
                vessage = new Vessage()
                {
                    Id = ObjectId.GenerateNewId(),
                    IsRead = false,
                    Sender = new ObjectId(UserSessionData.UserId),
                    VideoReady = false,
                    SendTime = DateTime.UtcNow,
                    ExtraInfo = extraInfo
                };
                result = await vessageService.SendVessage(new ObjectId(receiverId), vessage);
            }
            if (result == null)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return new { msg = "FAIL" };
            }

            return new
            {
                vessageBoxId = result.Item1.ToString(),
                vessageId = result.Item2.ToString()
            };
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
