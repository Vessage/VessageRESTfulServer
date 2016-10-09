using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BahamutCommon;
using MongoDB.Driver;
using MongoDB.Bson;
using VessageRESTfulServer.Services;

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
            IEnumerable<NFCPost> posts = await postCol.Find(f => f.Type == NFCPost.TYPE_NORMAL).SortByDescending(p => p.PostTs).Limit(postCnt).ToListAsync();

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
            var posts = await postCol.Find(f => f.UserId == UserObjectId && f.State > 0 && f.PostTs < ts).SortByDescending(p => p.PostTs).Limit(cnt).ToListAsync();
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

        [HttpGet("PostComments")]
        public async Task<IEnumerable<object>> GetPostComments(string postId, long ts)
        {
            var postCmtCol = NiceFaceClubDb.GetCollection<NFCPostComment>("NFCPostComment");
            var cmts = await postCmtCol.Find(c => c.PostId == new ObjectId(postId) && c.PostTs > ts).SortBy(c => c.PostTs).Limit(30).ToListAsync();
            var res = from c in cmts
                      select new
                      {
                          cmt = c.Content,
                          ts = c.PostTs,
                          psterNk = c.PosterNick,
                          pster = c.Poster.ToString()
                      };
            return res;
        }

        [HttpPost("LikePost")]
        public async Task<object> LikePost(string postId)
        {
            if (await LikePost(postId,1))
            {
                return new { msg = "SUCCESS" };
            }
            else
            {
                Response.StatusCode = 400;
                return null;
            }
            
        }

        private async Task<bool> LikePost(string postId, int likesCount)
        {
            var usrCol = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var likeCol = NiceFaceClubDb.GetCollection<NFCPostLike>("NFCPostLike");
            var postCol = NiceFaceClubDb.GetCollection<NFCPost>("NFCPost");
            try
            {
                var post = await postCol.Find(p => p.Id == new ObjectId(postId) && p.State > 0).FirstAsync();
                var nowTs = (long)DateTimeUtil.UnixTimeSpan.TotalMilliseconds;
                var opt = new FindOneAndUpdateOptions<NFCPostLike, NFCPostLike>();
                opt.ReturnDocument = ReturnDocument.Before;
                opt.IsUpsert = true;
                opt.Projection = new ProjectionDefinitionBuilder<NFCPostLike>().Include(l => l.Ts);
                var filter = new FilterDefinitionBuilder<NFCPostLike>().Where(l => l.PostId == post.Id && l.UserId == UserObjectId);
                var updateLike = new UpdateDefinitionBuilder<NFCPostLike>().Set(f => f.Ts, nowTs).Set(l => l.PostId, post.Id).Set(l => l.UserId, UserObjectId);
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
                        
                    var updatePost = new UpdateDefinitionBuilder<NFCPost>().Set(p => p.UpdateTs, (long)DateTimeUtil.UnixTimeSpan.TotalMilliseconds).Inc(p => p.Likes, 1);
                    if (usr.Likes > NiceFaceClubConfigCenter.BaseLikeJoinNFC && usr.ProfileState > 0 && usr.ProfileState != NFCMemberProfile.STATE_VALIDATED)
                    {
                        await usrCol.UpdateOneAsync(f => f.Id == post.MemberId, new UpdateDefinitionBuilder<NFCMemberProfile>().Set(f => f.ProfileState, NFCMemberProfile.STATE_VALIDATED));
                        updatePost.Set(p => p.Type, NFCPost.TYPE_NORMAL);
                    }
                    await postCol.UpdateOneAsync(p => p.Id == new ObjectId(postId), updatePost);
                    if(post.MemberId != usr.Id)
                    {
                        AppServiceProvider.GetActivityService().AddActivityBadge(NiceFaceClubConfigCenter.ActivityId, usr.UserId.ToString(), 1);
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [HttpPost("PostComments")]
        public async Task<object> NewPostComment(string postId, string comment)
        {
            var nowTs = (long)DateTimeUtil.UnixTimeSpan.TotalMilliseconds;
            var postCol = NiceFaceClubDb.GetCollection<NFCPost>("NFCPost");

            var postCmtCol = NiceFaceClubDb.GetCollection<NFCPostComment>("NFCPostComment");
            var usrCol = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");

            var post = await postCol.FindOneAndUpdateAsync(p => p.Id == new ObjectId(postId) && p.State > 0, new UpdateDefinitionBuilder<NFCPost>().Set(p => p.UpdateTs, nowTs).Inc(p => p.Cmts, 1));

            var cmtPoster = await usrCol.Find(p => p.UserId == UserObjectId && p.ProfileState == NFCMemberProfile.STATE_VALIDATED).Project(t => new { Nick = t.Nick, Id = t.Id }).FirstAsync();

            var newCmt = new NFCPostComment
            {
                Content = comment,
                Poster = cmtPoster.Id,
                PosterNick = cmtPoster.Nick,
                PostId = post.Id,
                PostTs = nowTs
            };

            await postCmtCol.InsertOneAsync(newCmt);

            var update = new UpdateDefinitionBuilder<NFCMemberProfile>().Inc(p => p.NewCmts, 1);
            await usrCol.UpdateOneAsync(x => x.Id == post.MemberId, update);
            if(cmtPoster.Id != post.MemberId){
                AppServiceProvider.GetActivityService().AddActivityBadge(NiceFaceClubConfigCenter.ActivityId, post.UserId.ToString(), 1);
            }
            return new
            {
                msg = "SUCCESS"
            };
        }
    }
}