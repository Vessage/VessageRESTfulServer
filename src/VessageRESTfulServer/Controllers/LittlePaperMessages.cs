using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using VessageRESTfulServer.Services;
using MongoDB.Driver;
using BahamutCommon;
using System.Net;
using BahamutService.Service;
using Newtonsoft.Json;
using System.Runtime;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{
    
    class LittlePaperMessage
    {
        public ObjectId Id{ get; set; }
        public string Sender { get; set; }
        public string ReceiverInfo { get; set; }
        public string Content { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime UpdatedTime { get; set; }
        public string Receiver { get; set; }
        public string[] Postmen { get; set; }
        public bool OpenNeedAccept { get; set; }
    }

    class PaperMessageBox
    {
        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }
        public ObjectId[] ReceivedMessages { get; set; }
    }

    class LittlePaperReadResponse
    {
        public const int TYPE_ASK_SENDER = 1;
        public const int TYPE_RETURN_ASKER = 2;

        public const int CODE_ACCEPT_READ = 1;
        public const int CODE_REJECT_READ = 2;
        public const int CODE_ASK_OPEN = 3;

        public ObjectId Id { get; set; }
        public ObjectId PaperId { get; set; }
        public ObjectId ToUser { get; set; }
        public string Asker { get; set; }
        public string AskerNick { get; set; }
        public string PaperReceiver { get; set; }
        public int Type { get; set; }
        public int Code { get; set; }

        public bool Got { get; set; }
    }

    [Route("api/[controller]")]
    public class LittlePaperMessages : APIControllerBase
    {
        public static string ActivityId = "1000";

        private IMongoDatabase LittlePaperDb
        {
            get
            {
                var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
                return client.GetDatabase("LittlePaperMessage");
            }
        }

        [HttpGet("Received")]
        public async Task<IEnumerable<object>> GetReceivedPapers()
        {

            var collection = LittlePaperDb.GetCollection<PaperMessageBox>("PaperMessageBox");
            try
            {
                var msgBox = await collection.Find(pb => pb.UserId == UserObjectId).FirstAsync();
                var msgCollection = LittlePaperDb.GetCollection<LittlePaperMessage>("LittlePaperMessage");
                var filter = new FilterDefinitionBuilder<LittlePaperMessage>().In(m => m.Id, msgBox.ReceivedMessages);
                var msgs = await msgCollection.Find(filter).ToListAsync();
                var msg = PaperMessageToJsonObject(new LittlePaperMessage(), UserSessionData.UserId);
                IEnumerable<object> result = from m in msgs select PaperMessageToJsonObject(m, UserSessionData.UserId);
                return result;
            }
            catch (Exception)
            {
                return new object[0];
            }
        }

        private object PaperMessageToJsonObject(LittlePaperMessage m, string userId)
        {

            var openForUser = m.Sender == UserSessionData.UserId || m.Receiver == UserSessionData.UserId;
            return new
            {
                paperId = m.Id.ToString(),
                sender = openForUser ? m.Sender : null,
                receiver = openForUser ? m.Receiver : null,
                receiverInfo = m.ReceiverInfo,
                message = openForUser ? m.Content : null,
                postmen = m.Postmen != null ? ArrayUtil.GetRandomArray(m.Postmen) : new string[0],
                updatedTime = DateTimeUtil.ToAccurateDateTimeString(m.UpdatedTime),
                isOpened = string.IsNullOrWhiteSpace(m.Receiver) == false
            };
        }

        [HttpGet]
        public async Task<IEnumerable<object>> GetPapersByIds(string paperIds)
        {
            var oids = from id in paperIds.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries) select new ObjectId(id);
            var msgCollection = LittlePaperDb.GetCollection<LittlePaperMessage>("LittlePaperMessage");
            var filter = new FilterDefinitionBuilder<LittlePaperMessage>().In(m => m.Id, oids);
            var msgs = await msgCollection.Find(filter).ToListAsync();
            var msg = PaperMessageToJsonObject(new LittlePaperMessage(), UserSessionData.UserId);
            IEnumerable<object> result = from m in msgs select PaperMessageToJsonObject(m, UserSessionData.UserId);
            return result;
        }

        [HttpPost]
        public async Task<object> NewPaperMessage(string receiverInfo, string message, string nextReceiver, bool openNeedAccept = true)
        {
            if (UserSessionData.UserId == nextReceiver)
            {
                Response.StatusCode = 404;
                return new { msg = "SELF_IS_RECEIVER" };
            }
            try
            {
                var receiverOId = new ObjectId(nextReceiver);
                var newMsg = new LittlePaperMessage()
                {
                    Content = message,
                    Postmen = new string[] { UserSessionData.UserId },
                    ReceiverInfo = receiverInfo,
                    OpenNeedAccept = openNeedAccept,
                    Sender = UserSessionData.UserId,
                    UpdatedTime = DateTime.UtcNow,
                    CreateTime = DateTime.UtcNow
                };
                var msgCollection = LittlePaperDb.GetCollection<LittlePaperMessage>("LittlePaperMessage");
                await msgCollection.InsertOneAsync(newMsg);
                var collection = LittlePaperDb.GetCollection<PaperMessageBox>("PaperMessageBox");
                var update = new UpdateDefinitionBuilder<PaperMessageBox>().Push(mb => mb.ReceivedMessages, newMsg.Id);
                var msgBox = await collection.FindOneAndUpdateAsync(pb => pb.UserId == receiverOId, update);
                if (msgBox == null)
                {
                    msgBox = new PaperMessageBox
                    {
                        ReceivedMessages = new ObjectId[] { newMsg.Id },
                        UserId = receiverOId
                    };
                    await collection.InsertOneAsync(msgBox);
                }
                AppServiceProvider.GetActivityService().AddActivityBadge(ActivityId, nextReceiver, 1);
                PublishActivityNotify(nextReceiver);
                return PaperMessageToJsonObject(newMsg, UserSessionData.UserId);
            }
            catch (Exception)
            { }
            Response.StatusCode = 500;
            return null;
        }

        private void PublishActivityNotify(string user)
        {
            var notifyMsg = new BahamutPublishModel
            {
                NotifyInfo = JsonConvert.SerializeObject(new
                {
                    BuilderId = 2,
                    AfterOpen = "go_custom",
                    Custom = "ActivityUpdatedNotify",
                    Text = UserSessionData.UserId,
                    LocKey = "ACTIVITY_UPDATED_NOTIFICATION"
                }),
                NotifyType = "ActivityUpdatedNotify",
                ToUser = user
            };
            AppServiceProvider.GetBahamutPubSubService().PublishVegeNotifyMessage(notifyMsg);
        }

        [HttpPut("PostMessage")]
        public async Task<object> PostMessageToNext(string paperId, string nextReceiver, bool isAnonymousPost = false)
        {
            var receiverOId = new ObjectId(nextReceiver);
            var paperOId = new ObjectId(paperId);

            if (UserSessionData.UserId == nextReceiver)
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return new { msg = "SELF_IS_RECEIVER" };
            }

            var msgCollection = LittlePaperDb.GetCollection<LittlePaperMessage>("LittlePaperMessage");
            try
            {
                var msg = await msgCollection.Find(m => m.Id == paperOId).FirstAsync();
                if (msg.Postmen.Contains(nextReceiver))
                {
                    Response.StatusCode = 403;
                    return new { msg = "USER_POSTED_THIS_PAPER" };
                }
            }
            catch (Exception)
            {
                Response.StatusCode = 400;
                return new { msg = "NO_SUCH_PAPER_ID" };
            }

            var collection = LittlePaperDb.GetCollection<PaperMessageBox>("PaperMessageBox");

            var updatePosterBox = new UpdateDefinitionBuilder<PaperMessageBox>().Pull(b => b.ReceivedMessages, paperOId);
            await collection.UpdateOneAsync(mbox => mbox.UserId == UserObjectId, updatePosterBox);

            var updateReceiverBox = new UpdateDefinitionBuilder<PaperMessageBox>().Push(mb => mb.ReceivedMessages, paperOId);
            var msgBox = await collection.FindOneAndUpdateAsync(pb => pb.UserId == receiverOId, updateReceiverBox);

            if (msgBox == null)
            {
                msgBox = new PaperMessageBox
                {
                    ReceivedMessages = new ObjectId[] { paperOId },
                    UserId = receiverOId
                };
                await collection.InsertOneAsync(msgBox);
            }

            var updateMsg = new UpdateDefinitionBuilder<LittlePaperMessage>().Set(m => m.UpdatedTime, DateTime.UtcNow);
            if (!isAnonymousPost)
            {
                var updateMsgPostmen = new UpdateDefinitionBuilder<LittlePaperMessage>().Push(m => m.Postmen, UserSessionData.UserId);
                updateMsg = new UpdateDefinitionBuilder<LittlePaperMessage>().Combine(updateMsg, updateMsgPostmen);
            }
            await msgCollection.UpdateOneAsync(m => m.Id == paperOId, updateMsg);
            AppServiceProvider.GetActivityService().AddActivityBadge(ActivityId, nextReceiver, 1);
            PublishActivityNotify(nextReceiver);
            return new { msg = "SUCCESS" };
        }

        [HttpPut("OpenPaperId/{paperId}")]
        public async Task<object> OpenAcceptLessPaperById(string paperId)
        {
            var paperOId = new ObjectId(paperId);
            var msgCollection = LittlePaperDb.GetCollection<LittlePaperMessage>("LittlePaperMessage");

            var collection = LittlePaperDb.GetCollection<PaperMessageBox>("PaperMessageBox");
            try
            {
                var msg = await msgCollection.Find(m => m.Id == paperOId).FirstAsync();
                if (string.IsNullOrWhiteSpace(msg.Receiver) == false)
                {
                    Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return new { msg = "PAPER_OPENED" };
                }

                if (msg.OpenNeedAccept)
                {
                    Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return new { msg = "PAPER_OPEN_NEED_SENDER_ACCEPT" };
                }

                var update = new UpdateDefinitionBuilder<PaperMessageBox>().Pull(mb => mb.ReceivedMessages, paperOId);
                var result = await collection.UpdateOneAsync(mbox => mbox.UserId == UserObjectId, update);
                var updateMsgTime = new UpdateDefinitionBuilder<LittlePaperMessage>().Set(m => m.UpdatedTime, DateTime.UtcNow);
                var updateMsgReceiver = new UpdateDefinitionBuilder<LittlePaperMessage>().Set(pm => pm.Receiver, UserSessionData.UserId);
                var updateMsg = new UpdateDefinitionBuilder<LittlePaperMessage>().Combine(updateMsgReceiver, updateMsgTime);
                result = await msgCollection.UpdateOneAsync(m => m.Id == paperOId, updateMsg);

                if (result.ModifiedCount > 0)
                {
                    msg.Receiver = UserSessionData.UserId;
                    return PaperMessageToJsonObject(msg, UserSessionData.UserId);
                }
                else
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return null;
                }

            }
            catch (Exception)
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return new { msg = "NO_SUCH_PAPER_ID" };
            }

        }

        [HttpPost("AskReadPaper")]
        public async Task<object> AskReadPaper(string paperId)
        {
            var paperOId = new ObjectId(paperId);
            var userOId = UserObjectId;
            var readerOId = UserObjectId;
            var readerId = UserSessionData.UserId;
            try
            {
                var msgCollection = LittlePaperDb.GetCollection<LittlePaperMessage>("LittlePaperMessage");
                var msg = await msgCollection.Find(m => m.Id == paperOId).FirstAsync();
                var reader = await AppServiceProvider.GetUserService().GetUserOfUserId(readerOId);
                if (string.IsNullOrWhiteSpace(msg.Receiver) == false)
                {
                    Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return new { msg = "PAPER_OPENED" };
                }
                var sender = msg.Sender;
                var senderOId = new ObjectId(sender);

                var responseCollection = LittlePaperDb.GetCollection<LittlePaperReadResponse>("LittlePaperReadResponse");
                var readResp = new LittlePaperReadResponse
                {
                    ToUser = senderOId,
                    Asker = readerId,
                    AskerNick = reader.Nick,
                    Got = false,
                    Code = LittlePaperReadResponse.CODE_ASK_OPEN,
                    PaperId = paperOId,
                    PaperReceiver = msg.ReceiverInfo,
                    Type = LittlePaperReadResponse.TYPE_ASK_SENDER
                };
                await responseCollection.InsertOneAsync(readResp);
                AppServiceProvider.GetActivityService().AddActivityBadge(ActivityId, sender, 1);
                PublishActivityNotify(sender);

                return new { msg = "REQUEST_SENDED" };
            }
            catch (Exception)
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return new { msg = "NO_SUCH_PAPER_ID" };
            }

        }

        [HttpPost("RejectReadPaper")]
        public async Task<object> RejectReadPaper(string reader, string paperId)
        {
            var paperOId = new ObjectId(paperId);
            var userOId = UserObjectId;
            var readerOId = new ObjectId(reader);
            try
            {
                var msgCollection = LittlePaperDb.GetCollection<LittlePaperMessage>("LittlePaperMessage");
                var msg = await msgCollection.Find(m => m.Id == paperOId && m.Sender == UserSessionData.UserId).FirstAsync();
                if (string.IsNullOrWhiteSpace(msg.Receiver) == false)
                {
                    Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return new { msg = "PAPER_OPENED" };
                }

                var responseCollection = LittlePaperDb.GetCollection<LittlePaperReadResponse>("LittlePaperReadResponse");
                var readResp = new LittlePaperReadResponse
                {
                    ToUser = readerOId,
                    Asker = reader,
                    Got = false,
                    Code = LittlePaperReadResponse.CODE_REJECT_READ,
                    PaperId = paperOId,
                    PaperReceiver = msg.ReceiverInfo,
                    Type = LittlePaperReadResponse.TYPE_RETURN_ASKER
                };
                await responseCollection.InsertOneAsync(readResp);
                AppServiceProvider.GetActivityService().AddActivityBadge(ActivityId, reader, 1);
                PublishActivityNotify(reader);

                return new { msg = "OPEN_PAPER_REJECT" };
            }
            catch (Exception)
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return new { msg = "NO_SUCH_PAPER_ID" };
            }


        }

        [HttpPost("AcceptReadPaper")]
        public async Task<object> AcceptReadPaper(string reader, string paperId)
        {
            var paperOId = new ObjectId(paperId);
            var userOId = UserObjectId;
            var readerOId = new ObjectId(reader);
            var msgCollection = LittlePaperDb.GetCollection<LittlePaperMessage>("LittlePaperMessage");

            var collection = LittlePaperDb.GetCollection<PaperMessageBox>("PaperMessageBox");
            try
            {
                var msg = await msgCollection.Find(m => m.Id == paperOId && m.Sender == UserSessionData.UserId).FirstAsync();
                if (string.IsNullOrWhiteSpace(msg.Receiver) == false)
                {
                    Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return new { msg = "OPEN_PAPER_ACCEPT" };
                }

                var update = new UpdateDefinitionBuilder<PaperMessageBox>().Pull(mb => mb.ReceivedMessages, paperOId);
                var result = await collection.UpdateOneAsync(mbox => mbox.UserId == readerOId, update);
                var updateMsgTime = new UpdateDefinitionBuilder<LittlePaperMessage>().Set(m => m.UpdatedTime, DateTime.UtcNow);
                var updateMsgReceiver = new UpdateDefinitionBuilder<LittlePaperMessage>().Set(pm => pm.Receiver, reader);
                var updateMsg = new UpdateDefinitionBuilder<LittlePaperMessage>().Combine(updateMsgReceiver, updateMsgTime);
                result = await msgCollection.UpdateOneAsync(m => m.Id == paperOId, updateMsg);

                var responseCollection = LittlePaperDb.GetCollection<LittlePaperReadResponse>("LittlePaperReadResponse");
                var readResp = new LittlePaperReadResponse
                {
                    ToUser = readerOId,
                    Asker = reader,
                    Got = false,
                    Code = LittlePaperReadResponse.CODE_ACCEPT_READ,
                    PaperId = paperOId,
                    PaperReceiver = msg.ReceiverInfo,
                    Type = LittlePaperReadResponse.TYPE_RETURN_ASKER
                };
                await responseCollection.InsertOneAsync(readResp);
                AppServiceProvider.GetActivityService().AddActivityBadge(ActivityId, reader, 1);
                PublishActivityNotify(reader);

                if (result.ModifiedCount > 0)
                {
                    return new { msg = "OPEN_PAPER_SUC" };
                }
                else
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return null;
                }
            }
            catch (Exception)
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return new { msg = "NO_SUCH_PAPER_ID" };
            }
        }

        [HttpGet("ReadPaperResponses")]
        public async Task<object> GetReadPaperResponses()
        {
            var responseCollection = LittlePaperDb.GetCollection<LittlePaperReadResponse>("LittlePaperReadResponse");
            var update = new UpdateDefinitionBuilder<LittlePaperReadResponse>()
                .Set(m => m.Got, true);
            await responseCollection.UpdateManyAsync(m => m.ToUser == UserObjectId, update);
            var resps = await responseCollection.Find(m => m.ToUser == UserObjectId).ToListAsync();
            
            var result = from r in resps
                         select new
                         {
                             paperId = r.PaperId.ToString(),
                             asker = r.Asker,
                             askerNick = r.AskerNick,
                             paperReceiver = r.PaperReceiver,
                             type = r.Type,
                             code = r.Code
                         };
            return result;
        }

        [HttpDelete("ClearGotResponses")]
        public async Task<object> ClearGotResponses()
        {
            var responseCollection = LittlePaperDb.GetCollection<LittlePaperReadResponse>("LittlePaperReadResponse");
            await responseCollection.DeleteManyAsync(m => m.ToUser == UserObjectId && m.Got);
            return new { msg = "CLEAR" };
        }
    }
}
