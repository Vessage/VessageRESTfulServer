using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using VessageRESTfulServer.Services;
using VessageRESTfulServer.Controllers;
using MongoDB.Bson;
using BahamutCommon;
using BahamutService.Service;
using Newtonsoft.Json;

namespace VessageRESTfulServer.Activities.AIViGi
{

    [Route("api/[controller]")]
    public partial class AIViGiMessageController : APIControllerBase
    {
        private IMongoDatabase MessageDb
        {
            get
            {
                var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
                return client.GetDatabase("AIViGiMessage");
            }
        }

        private IMongoDatabase AiViGiSNSDb
        {
            get
            {
                var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
                return client.GetDatabase("AIViGiSNS");
            }
        }

        [HttpGet("NewMessages")]
        public async Task<object> GetNewMessagesAsync(long lstFetchTs = 0)
        {
            var update = new UpdateDefinitionBuilder<AIMessage>().Set(f => f.State, AIMessage.STATE_RECEIVED);
            var col = MessageDb.GetCollection<AIMessage>("AIMessage");
            var lastFetchDate = DateTimeUtil.UnixTimeSpanZeroDate().AddMilliseconds(lstFetchTs);
            await col.UpdateManyAsync(f => f.Receiver == UserSessionData.UserId && f.State >= 0 && f.SendTime <= lastFetchDate, update);
            var messages = await col.Find(f => f.Receiver == UserSessionData.UserId && f.State >= 0 && f.SendTime > lastFetchDate).SortByDescending(f => f.SendTime).ToListAsync();
            if (messages.Count > 0)
            {
                return new
                {
                    id = "order",
                    newts = messages.First() == null ? 0 : DateTimeUtil.UnixTimeSpanOfDateTimeMs(messages.First().SendTime),
                    lasts = messages.Last() == null ? 0 : DateTimeUtil.UnixTimeSpanOfDateTimeMs(messages.Last().SendTime),
                    msgs = from m in messages
                           select new
                           {
                               mid = m.Id.ToString(),
                               bodyType = m.BodyType,
                               body = m.Body,
                               sender = m.Sender.ToString(),
                               snoteName = m.SNoteName,
                               ts = DateTimeUtil.UnixTimeSpanOfDateTimeMs(m.SendTime)
                           }
                };
            }
            else
            {
                return new { id = "order", newts = 0, lasts = 0, msgs = new AIMessage[0] };
            }
        }

        [HttpPost("Messages")]
        public async Task<object> SendMessage(string receiver, int bodyType, string body)
        {
            var col = MessageDb.GetCollection<AIMessage>("AIMessage");
            var noteName = await MessageDb.GetCollection<AISNSFocus>("AISNSFocus").Find(f => f.UserId == new ObjectId(receiver) && f.FocusedUserId == UserObjectId && f.Linked)
            .Project(f => f.FocusedNoteName).FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(noteName))
            {
                return new
                {
                    code = 404,
                    msg = "NOT_LINKED_USER"
                };
            }
            var newmsg = new AIMessage
            {
                BodyType = bodyType,
                Body = body,
                Sender = UserObjectId,
                SNoteName = noteName,
                Receiver = receiver,
                SendTime = DateTime.UtcNow,
                State = AIMessage.STATE_NORMAL
            };
            await col.InsertOneAsync(newmsg);
            var notification = new BahamutPublishModel
            {
                NotifyInfo = JsonConvert.SerializeObject(new
                {
                    BuilderId = 0,
                    AfterOpen = "go_custom",
                    Custom = "ViGiNewMessage",
                    LocKey = String.Format("{0}发来一条消息", noteName),
                }, Formatting.None),
                NotifyType = "ViGiNewMessage",
                ToUser = receiver
            };

            AppServiceProvider.GetBahamutPubSubService().PublishVegeNotifyMessage(notification);
            return new
            {
                code = 200,
                msg = "SUCCESS"
            };
        }
    }
}