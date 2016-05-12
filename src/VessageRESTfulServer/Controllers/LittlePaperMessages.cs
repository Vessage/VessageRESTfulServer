using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using MongoDB.Bson;
using VessageRESTfulServer.Services;
using MongoDB.Driver;
using BahamutCommon;
using System.Net;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{

    class LittlePaperMessage
    {
        public ObjectId Id{ get; set; }
        public string Sender { get; set; }
        public string ReceiverInfo { get; set; }
        public string Content { get; set; }
        public DateTime UpdatedTime { get; set; }
        public string Receiver { get; set; }
        public string[] Postmen { get; set; }
    }

    class PaperMessageBox
    {
        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }
        public ObjectId[] ReceivedMessages { get; set; }
    }

    [Route("api/[controller]")]
    public class LittlePaperMessages : APIControllerBase
    {
        
        [HttpGet("Received")]
        public async Task<IEnumerable<object>> Get()
        {
            var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
            var collection = client.GetDatabase("LittlePaperMessage").GetCollection<PaperMessageBox>("PaperMessageBox");
            try
            {
                var msgBox = await collection.Find(pb => pb.UserId == UserObjectId).FirstAsync();
                var msgCollection = client.GetDatabase("LittlePaperMessage").GetCollection<LittlePaperMessage>("LittlePaperMessage");
                var filter = new FilterDefinitionBuilder<LittlePaperMessage>().In(m => m.Id, msgBox.ReceivedMessages);
                var msgs = await msgCollection.Find(filter).ToListAsync();
                var msg = PaperMessageToJsonObject(new LittlePaperMessage(), UserSessionData.UserId);
                IEnumerable<object> result = from m in msgs select PaperMessageToJsonObject(m, UserSessionData.UserId);
                return result;
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.Message);
#endif
                return new object[0];
            }
        }

        private object PaperMessageToJsonObject(LittlePaperMessage m, string userId)
        {
            
            var openForUser = m.Sender == UserSessionData.UserId || m.Receiver == UserSessionData.UserId;
            return new
            {
                paperId = m.Id.ToString(),
                sender =  openForUser ? m.Sender : null,
                receiver = openForUser ? m.Receiver : null,
                receiverInfo = m.ReceiverInfo,
                message = openForUser ? m.Content : null,
                postmen = m.Postmen != null ? ArrayUtil.GetRandomArray(m.Postmen) : new string[0],
                updatedTime = DateTimeUtil.ToAccurateDateTimeString(m.UpdatedTime),
                isOpened = string.IsNullOrWhiteSpace(m.Receiver) == false
            };
        }

        [HttpGet]
        public async Task<IEnumerable<object>> Get(string paperIds)
        {
            var oids = from id in paperIds.Split(new char[] { ',',';' },StringSplitOptions.RemoveEmptyEntries) select new ObjectId(id);
            var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
            var msgCollection = client.GetDatabase("LittlePaperMessage").GetCollection<LittlePaperMessage>("LittlePaperMessage");
            var filter = new FilterDefinitionBuilder<LittlePaperMessage>().In(m => m.Id,oids);
            var msgs = await msgCollection.Find(filter).ToListAsync();
            var msg = PaperMessageToJsonObject(new LittlePaperMessage(), UserSessionData.UserId);
            IEnumerable<object> result = from m in msgs select PaperMessageToJsonObject(m, UserSessionData.UserId);
            return result;
        }

        [HttpPost]
        public async Task<object> Post(string receiverInfo, string message, string nextReceiver)
        {
            if (UserSessionData.UserId == nextReceiver)
            {
                Response.StatusCode = 404;
                return new { msg = "SELF_IS_RECEIVER" };
            }
            try
            {
                var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
                var receiverOId = new ObjectId(nextReceiver);
                var newMsg = new LittlePaperMessage()
                {
                    Content = message,
                    Postmen = new string[] { UserSessionData.UserId },
                    ReceiverInfo = receiverInfo,
                    Sender = UserSessionData.UserId,
                    UpdatedTime = DateTime.UtcNow
                };
                var msgCollection = client.GetDatabase("LittlePaperMessage").GetCollection<LittlePaperMessage>("LittlePaperMessage");
                await msgCollection.InsertOneAsync(newMsg);
                var collection = client.GetDatabase("LittlePaperMessage").GetCollection<PaperMessageBox>("PaperMessageBox");
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
                return PaperMessageToJsonObject(newMsg, UserSessionData.UserId);
            }
            catch (Exception)
            {}
            Response.StatusCode = 500;
            return null;
        }

        [HttpPut("PostMessage")]
        public async Task<object> Put(string paperId, string nextReceiver, string isAnonymousPost = "false")
        {
            var receiverOId = new ObjectId(nextReceiver);
            var paperOId = new ObjectId(paperId);

            if (UserSessionData.UserId == nextReceiver)
            {
                Response.StatusCode = 404;
                return new { msg = "SELF_IS_RECEIVER" };
            }
            bool isAnonymous = false;
            try
            {
                isAnonymous = bool.Parse(isAnonymousPost);
            }
            catch (Exception) { }

            var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
            
            var msgCollection = client.GetDatabase("LittlePaperMessage").GetCollection<LittlePaperMessage>("LittlePaperMessage");
            try
            {
                var msg = await msgCollection.Find(m => m.Id == paperOId).FirstAsync();
                if (msg.Postmen.Contains(nextReceiver))
                {
                    Response.StatusCode = 404;
                    return new { msg = "USER_POSTED_THIS_MSG" };
                }
            }
            catch (Exception)
            {
                Response.StatusCode = 400;
                return new { msg = "MSG_NOT_FOUND" };
            }
            
            var collection = client.GetDatabase("LittlePaperMessage").GetCollection<PaperMessageBox>("PaperMessageBox");

            var updatePosterBox = new UpdateDefinitionBuilder<PaperMessageBox>().PullFilter(mb => mb.ReceivedMessages, id => id == paperOId);
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
            if (isAnonymous == false)
            {
                var updateMsgPostmen = new UpdateDefinitionBuilder<LittlePaperMessage>().Push(m => m.Postmen, UserSessionData.UserId);
                updateMsg = new UpdateDefinitionBuilder<LittlePaperMessage>().Combine(updateMsg, updateMsgPostmen);
            }
            await msgCollection.UpdateOneAsync(m => m.Id == paperOId, updateMsg);
            return new { msg = "SUCCESS" };
        }

        [HttpPut("OpenPaperId/{paperId}")]
        public async Task<object> Put(string paperId)
        {
            var paperOId = new ObjectId(paperId);
            var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
            var msgCollection = client.GetDatabase("LittlePaperMessage").GetCollection<LittlePaperMessage>("LittlePaperMessage");
            
            var collection = client.GetDatabase("LittlePaperMessage").GetCollection<PaperMessageBox>("PaperMessageBox");
            try
            {
                var msg = await msgCollection.Find(m => m.Id == paperOId).FirstAsync();
                if(string.IsNullOrWhiteSpace(msg.Receiver) == false)
                {
                    Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return new { msg = "PAPER_OPENED" };
                }

                var update = new UpdateDefinitionBuilder<PaperMessageBox>().PullFilter(mb => mb.ReceivedMessages, id => id == paperOId);
                var result = await collection.UpdateOneAsync(mbox => mbox.UserId == UserObjectId, update);

                var updateMsg = new UpdateDefinitionBuilder<LittlePaperMessage>().Set(pm => pm.Receiver, UserSessionData.UserId);
                result = await msgCollection.UpdateOneAsync(m => m.Id == paperOId, updateMsg);

                if (result.ModifiedCount > 0)
                {
					msg.Receiver = UserSessionData.UserId;
                    return PaperMessageToJsonObject(msg,UserSessionData.UserId);
                }
                else
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return null;
                }

            }
            catch (Exception)
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return new { msg = "NO_SUCH_PAPER_ID" };
            }
            
        }
    }
}
