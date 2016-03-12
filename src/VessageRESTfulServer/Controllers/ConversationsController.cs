using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using VessageRESTfulServer.Models;
using VessageRESTfulServer.Services;
using BahamutCommon;
using System.Net;
using MongoDB.Bson;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{
    [Route("api/[controller]")]
    public class ConversationsController : APIControllerBase
    {
        // GET: api/values
        [HttpGet("ConversationList")]
        public async Task<IEnumerable<object>> GetConversationList()
        {
            IEnumerable<Conversation> list = await Startup.ServicesProvider.GetConversationService().GetUserConversations(UserSessionData.UserId);
            return from c in list select GenerateConversationJsonObject(c);
        }

        private object GenerateConversationJsonObject(Conversation c)
        {
            return new
            {
                conversationId = c.Id.ToString(),
                chatterNoteName = c.NoteName,
                chatterId = c.ChattingUserId.ToString(),
                chatterMobile = c.ChattingUserMobile,
                lastMessageTime = DateTimeUtil.ToAccurateDateTimeString(c.LastMessageDateTime),

            };
        }

        // POST api/values
        [HttpPost]
        public async void Post(string userId, string mobile)
        {
            var conversation = new Conversation()
            {
                ChattingUserMobile = mobile
            };
            if (string.IsNullOrWhiteSpace(userId) == false)
            {
                conversation.ChattingUserId = new ObjectId(userId);
            }
            if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(mobile) == false)
            {
                var userService = Startup.ServicesProvider.GetUserService();
                var user = await userService.GetUserOfMobile(mobile);
                userId = user.Id.ToString();
            }
            conversation = await Startup.ServicesProvider.GetConversationService().AddConversation(UserSessionData.UserId, conversation);
            if (conversation == null)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
        }

        // PUT api/values/5
        [HttpPut("NoteName")]
        public async void Put(string conversationId, string noteName)
        {
            var suc = false;
            if (!string.IsNullOrWhiteSpace(noteName))
            {
                suc = await Startup.ServicesProvider.GetConversationService().ChangeConversationNoteName(UserSessionData.UserId, conversationId, noteName);   
            }
            if (!suc)
            {
                Response.StatusCode = (int)HttpStatusCode.NotModified;
            }
        }

        // DELETE api/values/5
        [HttpDelete]
        public async void Delete(string conversationId)
        {
            bool suc = await Startup.ServicesProvider.GetConversationService().RemoveConversation(UserSessionData.UserId, conversationId);
            if (suc == false)
            {
                Response.StatusCode = (int)HttpStatusCode.NotModified;
            }
        }
    }
}
