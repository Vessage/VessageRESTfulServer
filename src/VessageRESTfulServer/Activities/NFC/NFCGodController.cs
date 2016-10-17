using BahamutCommon;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VessageRESTfulServer.Services;

namespace VessageRESTfulServer.Activities.NFC
{
    public partial class NiceFaceClubController
    {
        private const string AccountRegexPattern = "^\\d{5}$";

        [HttpDelete("GodDeletePost")]
        public async void GodDeletePost(string pstId)
        {
            var user = await AppServiceProvider.GetUserService().GetUserOfUserId(UserObjectId);
            if (Regex.IsMatch(user.AccountId, AccountRegexPattern))
            {
                var postCol = NiceFaceClubDb.GetCollection<NFCPost>("NFCPost");
                await postCol.UpdateOneAsync(p => p.Id == new ObjectId(pstId) && p.State > 0, new UpdateDefinitionBuilder<NFCPost>().Set(p => p.State, NFCPost.STATE_DELETED));
                NLog.LogManager.GetLogger("Info").Info("Account:{0} Request NFC God Delete Method",user.AccountId);
            }
            else
            {
                NLog.LogManager.GetLogger("Warn").Warn("Account:{0} Try Request NFC God Method", user.AccountId);
            }
        }

        [HttpPost("GodLikePost")]
        public async void GodLikePost(string pstId)
        {
            var likesCount = 10;
            var user = await AppServiceProvider.GetUserService().GetUserOfUserId(UserObjectId);
            if (Regex.IsMatch(user.AccountId, AccountRegexPattern))
            {
                await LikePost(pstId, likesCount, null,user.Nick);
                NLog.LogManager.GetLogger("Info").Info("Account:{0} Request NFC God Like Method", user.AccountId);
            }
            else
            {
                NLog.LogManager.GetLogger("Warn").Warn("Account:{0} Try Request NFC God Method", user.AccountId);
            }
        }

        [HttpPost("GodBlockMember")]
        public async void GodBlockMember(string mbId)
        {
            var user = await AppServiceProvider.GetUserService().GetUserOfUserId(UserObjectId);
            if (Regex.IsMatch(user.AccountId, AccountRegexPattern))
            {
                var usrCol = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
                await usrCol.UpdateOneAsync(p => p.Id == new ObjectId(mbId), new UpdateDefinitionBuilder<NFCMemberProfile>().Set(p => p.ProfileState, NFCMemberProfile.STATE_BLACK_LIST));
                NLog.LogManager.GetLogger("Info").Info("Account:{0} Request NFC God Block Member Method", user.AccountId);
            }
            else
            {
                NLog.LogManager.GetLogger("Warn").Warn("Account:{0} Try Request NFC God Method", user.AccountId);
            }
        }
    }
}
