using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VessageRESTfulServer.Models;
using MongoDB.Bson;
using BahamutCommon;
using VessageRESTfulServer.Services;
using System.Net;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{
    [Route("api/[controller]")]
    public class GroupChatsController : APIControllerBase
    {

        [HttpGet]
        public async Task<object> GetGroupChat(string groupId)
        {
            var g = await AppServiceProvider.GetGroupChatService().GetChatGroupById(new ObjectId(groupId));
            return ChatGroupToJsonObject(g);
        }

        private object ChatGroupToJsonObject(ChatGroup g)
        {
            return new
            {
                groupId = g.Id.ToString(),
                hosters = from hoster in g.Hosters select hoster.ToString(),
                chatters = from chatter in g.Chatters select chatter.ToString(),
                inviteCode = g.InviteCode
            };
        }

        [HttpPost("CreateGroupChat")]
        public async Task<object> CreateGroupChat(string groupUsers)
        {
            var userIds = from id in groupUsers.Split(new char[] { ',', ';' }) select new ObjectId(id);
            var g = await AppServiceProvider.GetGroupChatService().CreateChatGroup(UserObjectId, userIds);
            return ChatGroupToJsonObject(g);
        }

        [HttpPost("JoinGroupChat")]
        public async Task<object> JoinGroupChat(string groupId, string inviteCode)
        {
            if (await AppServiceProvider.GetGroupChatService().UserJoinGroup(UserObjectId, new ObjectId(groupId), inviteCode))
            {
                return new
                {
                    msg = "SUCCESS"
                };
            }
            Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return new
            {
                msg = "FAIL"
            };
        }

        [HttpDelete("QuitGroupChat")]
        public async Task<object> QuitGroupChat(string groupId)
        {
            if (await AppServiceProvider.GetGroupChatService().QuitChatGroup(UserObjectId, new ObjectId(groupId)))
            {
                return new
                {
                    msg = "SUCCESS"
                };
            }
            Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return new
            {
                msg = "FAIL"
            };
        }

        [HttpDelete("KickUserOut")]
        public async Task<object> KickUserOut(string groupId, string userId)
        {
            if (await AppServiceProvider.GetGroupChatService().KickUserFromChatGroup(UserObjectId, new ObjectId(groupId), new ObjectId(userId)))
            {
                return new
                {
                    msg = "SUCCESS"
                };
            }
            Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return new
            {
                msg = "FAIL"
            };
        }

        [HttpPut("EditGroupName")]
        public async Task<object> EditGroupName(string groupId, string inviteCode, string newGroupName)
        {
            if (await AppServiceProvider.GetGroupChatService().EditGroupName(new ObjectId(groupId),inviteCode,newGroupName))
            {
                return new
                {
                    msg = "SUCCESS"
                };
            }
            Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return new
            {
                msg = "FAIL"
            };
        }
    }
}
