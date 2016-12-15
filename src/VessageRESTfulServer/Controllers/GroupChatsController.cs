using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VessageRESTfulServer.Models;
using MongoDB.Bson;
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
            var g = await AppServiceProvider.GetGroupChatService().GetChatGroupById(UserObjectId, new ObjectId(groupId));
            return ChatGroupToJsonObject(g);
        }

        private object ChatGroupToJsonObject(ChatGroup g)
        {
            if (g == null)
            {
                Response.StatusCode = 400;
                return null;
            }
            return new
            {
                groupId = g.Id.ToString(),
                hosters = from hoster in g.Hosters select hoster.ToString(),
                chatters = from chatter in g.Chatters select chatter.ToString(),
                inviteCode = g.InviteCode,
                groupName = g.GroupName
            };
        }

        [HttpPost("CreateGroupChat")]
        public async Task<object> CreateGroupChat(string groupUsers, string groupName)
        {

            var userIdArray = groupUsers.Split(new char[] { ',', ';' });
            if (userIdArray.Count() > 0)
            {
                userIdArray = new HashSet<string>(userIdArray).ToArray();
                var userIds = from id in userIdArray select new ObjectId(id);
                var g = await AppServiceProvider.GetGroupChatService().CreateChatGroup(UserObjectId, userIds, groupName);
                return ChatGroupToJsonObject(g);
            }
            else
            {
                Response.StatusCode = 400;
                return null;
            }
        }

        [HttpPost("AddUserJoinGroupChat")]
        public async Task<object> AddUserJoinGroupChat(string groupId, string userId)
        {
            if (await AppServiceProvider.GetGroupChatService().AddUserJoinGroup(UserObjectId, new ObjectId(groupId), new ObjectId(userId)))
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
            if (await AppServiceProvider.GetGroupChatService().EditGroupName(new ObjectId(groupId), inviteCode, newGroupName))
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
