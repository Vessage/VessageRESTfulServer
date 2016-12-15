using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;
using VessageRESTfulServer.Services;

namespace VessageRESTfulServer.Activities.SNS
{
    public partial class SNSController
    {
        private const string AccountRegexPattern = "^\\d{5}$";

        [HttpDelete("GodDeletePost")]
        public async void GodDeletePost(string pstId)
        {
            var user = await AppServiceProvider.GetUserService().GetUserOfUserId(UserObjectId);
            if (Regex.IsMatch(user.AccountId, AccountRegexPattern))
            {
                var postCol = SNSDb.GetCollection<SNSPost>("SNSPost");
                await postCol.UpdateOneAsync(p => p.Id == new ObjectId(pstId) && p.State > 0, new UpdateDefinitionBuilder<SNSPost>().Set(p => p.State, SNSPost.STATE_DELETED));
                NLog.LogManager.GetLogger("Info").Info("Account:{0} Request SNS God Delete Method",user.AccountId);
            }
            else
            {
                NLog.LogManager.GetLogger("Warn").Warn("Account:{0} Try Request SNS God Method", user.AccountId);
            }
        }

        [HttpPost("GodLikePost")]
        public async void GodLikePost(string pstId)
        {
            var likesCount = 10;
            var user = await AppServiceProvider.GetUserService().GetUserOfUserId(UserObjectId);
            if (Regex.IsMatch(user.AccountId, AccountRegexPattern))
            {
                await LikePost(pstId, likesCount,user.Nick);
                NLog.LogManager.GetLogger("Info").Info("Account:{0} Request SNS God Like Method", user.AccountId);
            }
            else
            {
                NLog.LogManager.GetLogger("Warn").Warn("Account:{0} Try Request SNS God Method", user.AccountId);
            }
        }

        [HttpPost("GodBlockMember")]
        public async void GodBlockMember(string mbId)
        {
            var user = await AppServiceProvider.GetUserService().GetUserOfUserId(UserObjectId);
            if (Regex.IsMatch(user.AccountId, AccountRegexPattern))
            {
                var usrCol = SNSDb.GetCollection<SNSMemberProfile>("SNSMemberProfile");
                await usrCol.UpdateOneAsync(p => p.Id == new ObjectId(mbId), new UpdateDefinitionBuilder<SNSMemberProfile>().Set(p => p.ProfileState, SNSMemberProfile.STATE_BLACK_LIST));
                NLog.LogManager.GetLogger("Info").Info("Account:{0} Request SNS God Block Member Method", user.AccountId);
            }
            else
            {
                NLog.LogManager.GetLogger("Warn").Warn("Account:{0} Try Request SNS God Method", user.AccountId);
            }
        }
    }
}
