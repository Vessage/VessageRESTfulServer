using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BahamutCommon;
using MongoDB.Driver;
using MongoDB.Bson;
using VessageRESTfulServer.Services;
using BahamutService.Service;
using Newtonsoft.Json;

namespace VessageRESTfulServer.Activities.NFC
{
    
    public partial class NiceFaceClubController
    {
        private static DateTime lastTimeCheckNewMember = DateTime.MinValue;
        private static long NewMember = 0;

        [HttpGet("NFCMainBoardData")]
        public async Task<object> GetNFCMainBoardData(int postCnt)
        {
            var usrCol = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            if (NewMember == 0 || (DateTime.UtcNow - lastTimeCheckNewMember).TotalMinutes > 10)
            {
                NewMember = await usrCol.Find(x => x.ProfileState == NFCMemberProfile.STATE_VALIDATING).CountAsync();
                lastTimeCheckNewMember = DateTime.UtcNow;
            }
            
            var postCol = NiceFaceClubDb.GetCollection<NFCPost>("NFCPost");
            IEnumerable<NFCPost> posts = await postCol.Find(f => f.Type == NFCPost.TYPE_NORMAL && f.State > 0).SortByDescending(p => p.PostTs).Limit(postCnt).ToListAsync();

            NFCMemberProfile profile = null;
            try
            {
                
                var pd = new ProjectionDefinitionBuilder<NFCMemberProfile>().Include(p => p.Likes).Include(p => p.NewLikes).Include(p => p.NewCmts);
                var opt = new FindOneAndUpdateOptions<NFCMemberProfile, NFCMemberProfile>
                {
                    ReturnDocument = ReturnDocument.Before,
                    Projection = pd,
                    IsUpsert = false
                };
                var filter = new FilterDefinitionBuilder<NFCMemberProfile>().Where(f => f.UserId == UserObjectId);
                var update = new UpdateDefinitionBuilder<NFCMemberProfile>().Set(p => p.NewLikes, 0).Set(p => p.NewCmts, 0);
                profile = await usrCol.FindOneAndUpdateAsync(filter, update, opt);
                if (profile == null || profile.Id == ObjectId.Empty)
                {
                    throw new NullReferenceException();
                }
            }
            catch (Exception)
            {
                profile = new NFCMemberProfile
                {
                    Likes = 0,
                    NewLikes = 0,
                    NewCmts = 0
                };
            }
            return new
            {
                tlks = profile.Likes,
                annc = NiceFaceClubConfigCenter.NFCAnnounce,
                newMemAnnc = NiceFaceClubConfigCenter.NFCJoinRuleAnnounce,
                nMemCnt = NewMember,
                nlks = profile.NewLikes,
                ncmt = profile.NewCmts,
                posts = from p in posts select NFCPostToJsonObject(p, NFCPost.TYPE_NORMAL)
            };
        }

        private static object NFCPostToJsonObject(NFCPost p, int type)
        {
            return new
            {
                pid = p.Id.ToString(),
                mbId = p.MemberId.ToString(),
                img = p.Image,
                ts = p.PostTs,
                upTs = p.UpdateTs,
                lc = p.Likes,
                cmtCnt = p.Cmts,
                t = type,
                pster = p.PosterNick
            };
        }

        [HttpGet("Posts")]
        public async Task<IEnumerable<object>> GetPosts(long ts, int cnt = 20)
        {
            var postCol = NiceFaceClubDb.GetCollection<NFCPost>("NFCPost");
            var posts = await postCol.Find(f => f.Type == NFCPost.TYPE_NORMAL && f.State > 0 && f.PostTs < ts).SortByDescending(p => p.PostTs).Limit(cnt).ToListAsync();
            return from p in posts select NFCPostToJsonObject(p, NFCPost.TYPE_NORMAL);
        }

        [HttpGet("NewMemberPost")]
        public async Task<object> GetNewMemberPost(long ts, int cnt)
        {
            var usrCol = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var postCol = NiceFaceClubDb.GetCollection<NFCPost>("NFCPost");
            try
            {
                var profileExists = await usrCol.Find(p => p.UserId == UserObjectId && p.ProfileState == NFCMemberProfile.STATE_VALIDATED).CountAsync();
                if (profileExists > 0)
                {
                    var posts = await postCol.Find(f => f.Type == NFCPost.TYPE_NEW_MEMBER && f.State > 0 && f.PostTs < ts).SortByDescending(p => p.PostTs).Limit(cnt).ToListAsync();
                    return from p in posts select NFCPostToJsonObject(p, NFCPost.TYPE_NEW_MEMBER);
                }
                else
                {
                    Response.StatusCode = 403;
                    return null;
                }
                
            }
            catch (Exception)
            {
                throw;
            }
            
        }

        [HttpGet("MyPost")]
        public async Task<object> GetMyPost(long ts, int cnt)
        {
            var postCol = NiceFaceClubDb.GetCollection<NFCPost>("NFCPost");
            var usrOId = UserObjectId;
            var posts = await postCol.Find(f => f.UserId == usrOId && f.State > 0 && f.UpdateTs < ts).SortByDescending(p => p.UpdateTs).Limit(cnt).ToListAsync();
            return from p in posts select NFCPostToJsonObject(p, NFCPost.TYPE_MY_POST);
        }

        [HttpGet("ChatMember")]
        public async Task<object> GetChatMember(string memberId)
        {
            var usrCol = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var userId = await usrCol.Find(p => p.Id == new ObjectId(memberId)).Project(p => p.UserId).FirstAsync();
            return new { userId = userId.ToString() };
        }

        [HttpPost("NewPost")]
        public async Task<object> NewPost(string image)
        {
            var usrCol = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var poster = await usrCol.Find(p => p.UserId == UserObjectId && p.ProfileState > 0).Project(p => new { Nick = p.Nick, MemberId = p.Id, State = p.ProfileState }).FirstAsync();
            var nowTs = (long)DateTimeUtil.UnixTimeSpan.TotalMilliseconds;
            var newPost = new NFCPost
            {
                Image = image,
                Likes = 0,
                Cmts = 0,
                MemberId = poster.MemberId,
                PosterNick = poster.Nick,
                PostTs = nowTs,
                UpdateTs = nowTs,
                Type = poster.State == NFCMemberProfile.STATE_VALIDATED ? NFCPost.TYPE_NORMAL : NFCPost.TYPE_NEW_MEMBER,
                UserId = UserObjectId,
                State = NFCPost.STATE_NORMAL
            };
            var postCol = NiceFaceClubDb.GetCollection<NFCPost>("NFCPost");
            await postCol.InsertOneAsync(newPost);
            return NFCPostToJsonObject(newPost, newPost.Type);
        }

        [HttpGet("ReceivedLikes")]
        public async Task<IEnumerable<object>> GetReceivedLikes(long ts,int cnt)
        {
            var likeCol = NiceFaceClubDb.GetCollection<NFCPostLike>("NFCPostLike");
            var likes = await likeCol.Find(lc=>lc.NFCPostUserId == UserObjectId && lc.Ts < ts).SortByDescending(l=>l.Ts).Limit(cnt).ToListAsync();
            var res = from l in likes select new 
            {
                ts = l.Ts,
                usrId = l.UserId.ToString(),
                nick = l.Nick,
                mbId = l.MemberId == ObjectId.Empty ? null : l.MemberId.ToString(),
                img = l.NFCPostImage
            };
            return res;
        }

        [HttpGet("MyComments")]
        public async Task<IEnumerable<object>> GetMyComments(long ts,int cnt)
        {
            var usrCol = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var member = await usrCol.Find(p => p.UserId == UserObjectId).Project(t => new { Id = t.Id }).FirstAsync();
            if(member == null)
            {
                Response.StatusCode = 404;
                return null;
            }
            var postCmtCol = NiceFaceClubDb.GetCollection<NFCPostComment>("NFCPostComment");
            var cmts = await postCmtCol.Find(c => (c.AtMemberId == member.Id || c.NFCPostPoster == member.Id || c.Poster == member.Id) && c.PostTs < ts).SortByDescending(c => c.PostTs).Limit(cnt).ToListAsync();
            var res = from c in cmts select NFCPostCommentToJsonObject(c,true);
            return res;
        }

        [HttpGet("PostComments")]
        public async Task<IEnumerable<object>> GetPostComments(string postId, long ts,int cnt = 30)
        {
            var postCmtCol = NiceFaceClubDb.GetCollection<NFCPostComment>("NFCPostComment");
            var cmts = await postCmtCol.Find(c => c.PostId == new ObjectId(postId) && c.PostTs > ts).SortBy(c => c.PostTs).Limit(cnt).ToListAsync();
            var res = from c in cmts select NFCPostCommentToJsonObject(c);
            return res;
        }

        private object NFCPostCommentToJsonObject(NFCPostComment c,bool includeImage = false){
            return new
                      {
                          postId = c.PostId.ToString(),
                          cmt = c.Content,
                          ts = c.PostTs,
                          psterNk = c.PosterNick,
                          pster = c.Poster.ToString(),
                          atNick = c.AtNick,
                          img = includeImage ? c.NFCPostImage : null
                      };
        }

        [HttpPost("LikePost")]
        public async Task<object> LikePost(string postId,string mbId = null,string nick = null)
        {
            if (await LikePost(postId,1,mbId,nick))
            {
                return new { msg = "SUCCESS" };
            }
            else
            {
                Response.StatusCode = 400;
                return null;
            }
        }

        private async Task<bool> LikePost(string postId, int likesCount,string memberId,string nick)
        {
            var usrCol = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var likeCol = NiceFaceClubDb.GetCollection<NFCPostLike>("NFCPostLike");
            var postCol = NiceFaceClubDb.GetCollection<NFCPost>("NFCPost");
            try
            {
                if(string.IsNullOrWhiteSpace(nick)){
                    nick = "NFCer";
                }
                var post = await postCol.Find(p => p.Id == new ObjectId(postId) && p.State > 0).FirstAsync();
                var nowTs = (long)DateTimeUtil.UnixTimeSpan.TotalMilliseconds;
                var opt = new FindOneAndUpdateOptions<NFCPostLike, NFCPostLike>();
                opt.ReturnDocument = ReturnDocument.Before;
                opt.IsUpsert = true;
                opt.Projection = new ProjectionDefinitionBuilder<NFCPostLike>().Include(l => l.Ts);
                var filter = new FilterDefinitionBuilder<NFCPostLike>().Where(l => l.PostId == post.Id && l.UserId == UserObjectId);
                var mbId = ObjectId.Empty;
                try{mbId = new ObjectId(memberId);}catch (System.Exception){}
                var updateLike = new UpdateDefinitionBuilder<NFCPostLike>()
                                    .Set(f => f.Ts, nowTs)
                                    .Set(l => l.PostId, post.Id)
                                    .Set(l => l.NFCPostUserId,post.UserId)
                                    .Set(l => l.Nick,nick)
                                    .Set(l => l.MemberId,mbId)
                                    .Set(l => l.NFCPostImage,post.Image)
                                    .Set(l => l.UserId, UserObjectId);
                var like = await likeCol.FindOneAndUpdateAsync(filter, updateLike, opt);
                if (like == null)
                {
                    var update = new UpdateDefinitionBuilder<NFCMemberProfile>().Inc(p => p.Likes, likesCount).Inc(p => p.NewLikes, likesCount);
                    var usrOpt = new FindOneAndUpdateOptions<NFCMemberProfile, NFCMemberProfile>
                    {
                        ReturnDocument = ReturnDocument.After,
                        Projection = new ProjectionDefinitionBuilder<NFCMemberProfile>().Include(p => p.Likes).Include(p => p.ProfileState).Include(p => p.UserId)
                    };
                    var usrFilter = new FilterDefinitionBuilder<NFCMemberProfile>().Where(f => f.Id == post.MemberId);
                    var usr = await usrCol.FindOneAndUpdateAsync(usrFilter, update, usrOpt);
                        
                    var updatePost = new UpdateDefinitionBuilder<NFCPost>().Set(p => p.UpdateTs, (long)DateTimeUtil.UnixTimeSpan.TotalMilliseconds).Inc(p => p.Likes, likesCount);
                    if (usr.Likes >= NiceFaceClubConfigCenter.BaseLikeJoinNFC && usr.ProfileState > 0 && usr.ProfileState != NFCMemberProfile.STATE_VALIDATED)
                    {
                        await usrCol.UpdateOneAsync(f => f.Id == post.MemberId, new UpdateDefinitionBuilder<NFCMemberProfile>().Set(f => f.ProfileState, NFCMemberProfile.STATE_VALIDATED));
                        updatePost.Set(p => p.Type, NFCPost.TYPE_NORMAL);
                        PublishActivityNotify(usr.UserId.ToString(), NiceFaceClubConfigCenter.NFCHelloMessage);
                    }
                    await postCol.UpdateOneAsync(p => p.Id == new ObjectId(postId), updatePost);
                    if(post.UserId != UserObjectId)
                    {
                        AppServiceProvider.GetActivityService().AddActivityBadge(NiceFaceClubConfigCenter.ActivityId, post.UserId, 1);
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void PublishActivityNotify(string user,string msgLocKey)
        {
            var notifyMsg = new BahamutPublishModel
            {
                NotifyInfo = JsonConvert.SerializeObject(new
                {
                    BuilderId = 2,
                    AfterOpen = "go_custom",
                    Custom = "ActivityUpdatedNotify",
                    Text = UserSessionData.UserId,
                    LocKey = msgLocKey
                }, Formatting.None),
                NotifyType = "ActivityUpdatedNotify",
                ToUser = user
            };
            AppServiceProvider.GetBahamutPubSubService().PublishVegeNotifyMessage(notifyMsg);
        }

        [HttpPost("PostComments")]
        public async Task<object> NewPostComment(string postId, string comment,string atMember = null,string atNick = null)
        {
            var nowTs = (long)DateTimeUtil.UnixTimeSpan.TotalMilliseconds;

            var postCol = NiceFaceClubDb.GetCollection<NFCPost>("NFCPost");
            var postFilter = new FilterDefinitionBuilder<NFCPost>().Where(p => p.Id == new ObjectId(postId) && p.State > 0);
            var postUpdate = new UpdateDefinitionBuilder<NFCPost>().Set(p => p.UpdateTs, nowTs).Inc(p => p.Cmts, 1);
            var postOpt = new FindOneAndUpdateOptions<NFCPost, NFCPost>
            {
                ReturnDocument = ReturnDocument.After,
                Projection = new ProjectionDefinitionBuilder<NFCPost>().Include(p => p.Id).Include(p => p.MemberId).Include(p => p.UserId).Include(p=>p.Image)
            };
            var post = await postCol.FindOneAndUpdateAsync(postFilter, postUpdate ,postOpt);

            var usrCol = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var cmtPoster = await usrCol.Find(p => p.UserId == UserObjectId && p.ProfileState == NFCMemberProfile.STATE_VALIDATED).Project(t => new { Nick = t.Nick, Id = t.Id }).FirstAsync();

            var newCmt = new NFCPostComment
            {
                Content = comment,
                Poster = cmtPoster.Id,
                PosterNick = cmtPoster.Nick,
                PostId = post.Id,
                PostTs = nowTs,
                NFCPostPoster = post.MemberId,
                NFCPostImage = post.Image
            };

            var badgedUserId = ObjectId.Empty;
            var activityService = AppServiceProvider.GetActivityService();
            if(!string.IsNullOrWhiteSpace(atMember)){
                ObjectId atMemberId;
                if(ObjectId.TryParse(atMember,out atMemberId)){
                    try {
                        var updateAtMember = new UpdateDefinitionBuilder<NFCMemberProfile>().Inc(p=>p.NewCmts,1);
                        var updateAtMemberOpt = new FindOneAndUpdateOptions<NFCMemberProfile, NFCMemberProfile>
                        {
                            ReturnDocument = ReturnDocument.After,
                            Projection = new ProjectionDefinitionBuilder<NFCMemberProfile>().Include(p => p.Id).Include(p => p.Nick).Include(p => p.UserId)
                        };
                        var updateAtMemberFilter = new FilterDefinitionBuilder<NFCMemberProfile>().Where(p=>p.Id == atMemberId);
                        var atMemberObj = await usrCol.FindOneAndUpdateAsync(updateAtMemberFilter,updateAtMember,updateAtMemberOpt);
                        if(atMemberObj != null){
                            newCmt.AtMemberId = atMemberId;
                            newCmt.AtNick = string.IsNullOrWhiteSpace(atNick) ? atMemberObj.Nick : atNick;
                            if(atMemberObj.UserId != UserObjectId){
                                activityService.AddActivityBadge(NiceFaceClubConfigCenter.ActivityId,atMemberObj.UserId,1);
                                badgedUserId = atMemberObj.UserId;
                            }
                        }
                    }catch(Exception){}
                }
            }

            var postCmtCol = NiceFaceClubDb.GetCollection<NFCPostComment>("NFCPostComment");
            await postCmtCol.InsertOneAsync(newCmt);

            if(cmtPoster.Id != post.MemberId){
                var update = new UpdateDefinitionBuilder<NFCMemberProfile>().Inc(p => p.NewCmts, 1);
                await usrCol.UpdateOneAsync(x => x.Id == post.MemberId, update);
                if(post.UserId != badgedUserId){
                    activityService.AddActivityBadge(NiceFaceClubConfigCenter.ActivityId, post.UserId, 1);
                }
            }
            return new
            {
                msg = "SUCCESS"
            };
        }
    }
}