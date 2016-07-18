using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VessageRESTfulServer.Services;
using VessageRESTfulServer.Models;
using BahamutService.Service;
using MongoDB.Bson;
using System.Net;
using BahamutCommon;
using Newtonsoft.Json;

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
                sendTime = DateTimeUtil.ToAccurateDateTimeString(m.SendTime),
                isGroup = m.IsGroup
            };
        }

        [HttpPut("Got")]
        public async Task GotNewVessages()
        {
            await AppServiceProvider.GetVessageService().UpdateGotMessageTime(UserSessionData.UserId);
        }

        
        [HttpPost("ForMobile")]
        public async Task<object> SendNewVessageForMobile(string receiverMobile, string extraInfo)
        {
            Vessage vessage = null;
            Tuple<ObjectId, ObjectId> result = null;
            var vessageService = Startup.ServicesProvider.GetVessageService();
            var userService = AppServiceProvider.GetUserService();
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
                var receiver = await userService.GetUserOfMobile(receiverMobile);
                if (receiver == null)
                {
                    receiver = await userService.CreateNewUserByMobile(receiverMobile);
                }

                if (receiver != null)
                {
                    result = await vessageService.SendVessage(receiver.Id, vessage, false);
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
        public async Task<object> SendNewVessageForUser(string receiverId, string extraInfo, bool isGroup = false)
        {
            Vessage vessage = null;
            Tuple<ObjectId, ObjectId> result = null;
            var receiverOId = new ObjectId(receiverId);
            var vessageService = AppServiceProvider.GetVessageService();
            if (isGroup)
            {
                var groupService = AppServiceProvider.GetGroupChatService();
                var group = await groupService.GetChatGroupById(UserObjectId,receiverOId);
                if (group == null)
                {
                    Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return new { msg = "NOT_IN__CHAT_GROUP" };
                }
            }
            if (string.IsNullOrWhiteSpace(receiverId) == false)
            {
                vessage = new Vessage()
                {
                    Id = ObjectId.GenerateNewId(),
                    IsRead = false,
                    Sender = isGroup ? receiverOId : UserObjectId,
                    VideoReady = false,
                    SendTime = DateTime.UtcNow,
                    ExtraInfo = extraInfo,
                    IsGroup = isGroup
                };
                result = await vessageService.SendVessage(receiverOId, vessage, isGroup);
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

        [HttpPut("FinishSendVessage")]
        public async Task<object> FinishSendVessage(string vessageBoxId, string vessageId, string fileId)
        {
            var vsgOId = new ObjectId(vessageId);
            var result = await AppServiceProvider.GetVessageService().FinishSendVessage(new ObjectId(vessageBoxId), UserObjectId, vsgOId, fileId);
            var msg = "SUCCESS";
            if (result == null)
            {
                Response.StatusCode = (int)HttpStatusCode.NotModified;
                return new { msg = "FAIL" };
            }
            else
            {
                if (result.ReceiverId != ObjectId.Empty)
                {
                    IEnumerable<ObjectId> toUsers;
                    string sender = UserSessionData.UserId;
                    if (result.ReceiverIsGroup)
                    {
                        ChatGroup chatGroup = await AppServiceProvider.GetGroupChatService().GetChatGroupById(UserObjectId, result.ReceiverId);
                        toUsers = from c in chatGroup.Chatters where c != UserObjectId select c;
                        await AppServiceProvider.GetVessageService().SendGroupVessageToChatters(chatGroup.Id, toUsers, vsgOId);
                        sender = chatGroup.Id.ToString();
                    }
                    else
                    {
                        toUsers = new ObjectId[] { result.ReceiverId };
                    }

                    foreach (var toUser in toUsers)
                    {
                        var notifyMsg = new BahamutPublishModel
                        {
                            NotifyInfo = JsonConvert.SerializeObject(new
                            {
                                BuilderId = 1,
                                AfterOpen = "go_custom",
                                Custom = "NewVessageNotify",
                                Text = sender,
                                LocKey = "NEW_VMSG_NOTIFICATION"
                            }),
                            NotifyType = "NewVessageNotify",
                            ToUser = toUser.ToString()
                        };
                        AppServiceProvider.GetBahamutPubSubService().PublishBahamutUserNotifyMessage("Vege", notifyMsg);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(result.ReceiverMobile))
                {
                    //TODO: Send sms to the mobile user
                }
            }
            return new { msg = msg };
        }

        [HttpPut("CancelSendVessage")]
        public async Task<object> CancelSendVessage(string vessageBoxId, string vessageId)
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
