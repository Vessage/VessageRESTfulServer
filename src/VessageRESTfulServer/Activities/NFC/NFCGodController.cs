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
        [HttpDelete("GodDeletePost")]
        public async void GodDeletePost(string pstId)
        {
            var user = await AppServiceProvider.GetUserService().GetUserOfUserId(UserObjectId);
            if (Regex.IsMatch(user.AccountId, "$1\\d{4}"))
            {
                var postCol = NiceFaceClubDb.GetCollection<NFCPost>("NFCPost");
                await postCol.UpdateOneAsync(p => p.Id == new ObjectId(pstId) && p.State > 0, new UpdateDefinitionBuilder<NFCPost>().Set(p => p.State, NFCPost.STATE_DELETED));
            }
        }

        [HttpPost("GodLikePost")]
        public async void GodLikePost(string pstId)
        {
            var user = await AppServiceProvider.GetUserService().GetUserOfUserId(UserObjectId);
            if (Regex.IsMatch(user.AccountId, "$1\\d{4}"))
            {
                await LikePost(pstId, 10);
            }
        }

        [HttpPost("GodBlockMember")]
        public async void GodBlockMember(string mbId)
        {
            var user = await AppServiceProvider.GetUserService().GetUserOfUserId(UserObjectId);
            if (Regex.IsMatch(user.AccountId, "$1\\d{4}"))
            {
                var usrCol = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
                await usrCol.UpdateOneAsync(p => p.Id == new ObjectId(mbId), new UpdateDefinitionBuilder<NFCMemberProfile>().Set(p => p.ProfileState, NFCMemberProfile.STATE_BLACK_LIST));
            }
            
        }
    }
}
