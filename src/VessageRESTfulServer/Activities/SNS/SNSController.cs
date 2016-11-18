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
using Microsoft.Extensions.Configuration;
using System.IO;
using VessageRESTfulServer.Controllers;

namespace VessageRESTfulServer.Activities.SNS
{
    public class SNSConfigCenter
    {
        public const string ActivityId = "1003"; 
        private static IConfiguration _snsConfig;
        private static IConfiguration SNSConfig
        {
            get
            {
                if (_snsConfig == null)
                {
                    var configRoot = Startup.ConfigRoot + Path.DirectorySeparatorChar;
                    _snsConfig = new ConfigurationBuilder()
                        .AddJsonFile(string.Format("{0}sns_config.json", configRoot), true, true)
                        .Build();
                }
                return _snsConfig;
            }
        }
        public static string SNSAnnounce { get { return SNSConfig["SNSAnnounce"]; } }
    }

    [Route("api/[controller]")]
    public partial class SNSController : APIControllerBase
    {
        private IMongoDatabase SNSDb
        {
            get
            {
                var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
                return client.GetDatabase("SNS");
            }
        }

        [HttpGet("SNSMainBoardData")]
        public async Task<object> GetSNSMainBoardData(int postCnt,string focusIds,string location = null)
        {
            var usrCol = SNSDb.GetCollection<SNSMemberProfile>("SNSMemberProfile");
            var postCol = SNSDb.GetCollection<SNSPost>("SNSPost");
            
            var focusOIds = string.IsNullOrWhiteSpace(focusIds) ? new ObjectId[0] : from id in focusIds.Split(new char[]{',',';'}) select new ObjectId(id); 

            var isNewer = false;
            SNSMemberProfile profile = null;
            try
            {
                var pd = new ProjectionDefinitionBuilder<SNSMemberProfile>().Include(p => p.Likes).Include(p => p.NewLikes).Include(p => p.NewCmts);
                var opt = new FindOneAndUpdateOptions<SNSMemberProfile, SNSMemberProfile>
                {
                    ReturnDocument = ReturnDocument.Before,
                    Projection = pd,
                    IsUpsert = false
                };
                var filter = new FilterDefinitionBuilder<SNSMemberProfile>().Where(f => f.UserId == UserObjectId);
                var update = new UpdateDefinitionBuilder<SNSMemberProfile>().Set(p => p.NewLikes, 0).Set(p => p.NewCmts, 0).Set(p => p.ActiveTime, DateTime.UtcNow);
                update = update.Set(p=>p.FocusUserIds,focusOIds.ToArray());
                if (!string.IsNullOrWhiteSpace(location))
                {
                    var c = Utils.LocationStringToLocation(location);
                    update = update.Set(p => p.Location, c);
                }
                profile = await usrCol.FindOneAndUpdateAsync(filter, update, opt);
                if (profile == null || profile.Id == ObjectId.Empty)
                {
                    throw new NullReferenceException();
                }
            }
            catch (Exception)
            {
                profile = new SNSMemberProfile
                {
                    UserId = UserObjectId,
                    Likes = 0,
                    NewLikes = 0,
                    NewCmts = 0,
                    ActiveTime = DateTime.UtcNow,
                    CreateTime = DateTime.UtcNow,
                    ProfileState = SNSMemberProfile.STATE_NORMAL,
                    FocusUserIds = focusOIds.ToArray(),
                    Location = string.IsNullOrWhiteSpace(location) ? null : Utils.LocationStringToLocation(location)
                };
                await usrCol.InsertOneAsync(profile);
                isNewer = true;
            }

            IEnumerable<SNSPost> posts = new SNSPost[0];
            if(focusOIds.Count() > 0)
            {
                var filter = new FilterDefinitionBuilder<SNSPost>().In(p=>p.UserId,focusOIds);
                var filter1 = new FilterDefinitionBuilder<SNSPost>().Where(p=>p.Type == SNSPost.TYPE_NORMAL && p.State > 0);
                posts = await postCol.Find(filter & filter1).SortByDescending(p => p.PostTs).Limit(postCnt).ToListAsync();
            }
            return new
            {
                tlks = profile.Likes,
                annc = SNSConfigCenter.SNSAnnounce,
                nlks = profile.NewLikes,
                ncmt = profile.NewCmts,
                newer = isNewer,
                posts = from p in posts select SNSPostToJsonObject(p, SNSPost.TYPE_NORMAL)
            };
        }

        private static object SNSPostToJsonObject(SNSPost p, int type)
        {
            return new
            {
                pid = p.Id.ToString(),
                usrId = p.UserId.ToString(),
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
            var postCol = SNSDb.GetCollection<SNSPost>("SNSPost");
            var usrCol = SNSDb.GetCollection<SNSMemberProfile>("SNSMemberProfile");
            var userIds = await usrCol.Find(u=>u.UserId == UserObjectId && u.ProfileState > 0).Project(u=>u.FocusUserIds).FirstAsync();
            var filter = new FilterDefinitionBuilder<SNSPost>().In(p=>p.UserId,userIds);
            var filter1 = new FilterDefinitionBuilder<SNSPost>().Where(p=>p.Type == SNSPost.TYPE_NORMAL && p.State > 0 && p.PostTs < ts);
            var posts = await postCol.Find(filter & filter1).SortByDescending(p => p.PostTs).Limit(cnt).ToListAsync();
            return from p in posts select SNSPostToJsonObject(p, SNSPost.TYPE_NORMAL);
        }

        [HttpDelete("Posts")]
        public async Task<object> DeletePost(string postId)
        {
            var postCol = SNSDb.GetCollection<SNSPost>("SNSPost");
            var update = new UpdateDefinitionBuilder<SNSPost>().Set(p=>p.State,SNSPost.STATE_REMOVED);
            var res = await postCol.UpdateOneAsync(f=>f.UserId == UserObjectId && f.Id == new ObjectId(postId),update);
            if(res.ModifiedCount > 0)
            {
                return new { msg = "SUCCESS" };
            }else
            {
                Response.StatusCode = 500;
                return new { msg = "FAIL" };
            }
            
        }

        [HttpPut("ObjectionablePosts")]
        public async Task<object> ObjectionablePosts(string postId)
        {
            var usrCol = SNSDb.GetCollection<SNSMemberProfile>("SNSMemberProfile");
            var profileExists = await usrCol.Find(p => p.UserId == UserObjectId && p.ProfileState == SNSMemberProfile.STATE_NORMAL).CountAsync();
            if (profileExists > 0)
            {
                Response.StatusCode = 403;
                return new { msg = "NOT_SNS_MEMBER" };
            }
            var postCol = SNSDb.GetCollection<SNSPost>("SNSPost");
            var update = new UpdateDefinitionBuilder<SNSPost>().Set(p=>p.State,SNSPost.STATE_IN_USER_OBJECTION);
            var res = await postCol.UpdateOneAsync(f=>f.Id == new ObjectId(postId),update);
            if(res.ModifiedCount > 0)
            {
                return new { msg = "SUCCESS" };
            }else
            {
                Response.StatusCode = 500;
                return new { msg = "FAIL" };
            }
        }

        [HttpGet("MyPost")]
        public async Task<object> GetMyPost(long ts, int cnt)
        {
            var postCol = SNSDb.GetCollection<SNSPost>("SNSPost");
            var usrOId = UserObjectId;
            var posts = await postCol.Find(f => f.UserId == usrOId && f.State > 0 && f.UpdateTs < ts).SortByDescending(p => p.UpdateTs).Limit(cnt).ToListAsync();
            return from p in posts select SNSPostToJsonObject(p, SNSPost.TYPE_MY_POST);
        }

        [HttpPost("NewPost")]
        public async Task<object> NewPost(string image,string nick)
        {
            var usrCol = SNSDb.GetCollection<SNSMemberProfile>("SNSMemberProfile");
            var poster = await usrCol.Find(p => p.UserId == UserObjectId && p.ProfileState > 0).CountAsync();
            if(poster == 0)
            {
                Response.StatusCode = 403;
                return null;
            }
            var nowTs = (long)DateTimeUtil.UnixTimeSpan.TotalMilliseconds;
            var newPost = new SNSPost
            {
                Image = image,
                Likes = 0,
                Cmts = 0,
                UserId = UserObjectId,
                PosterNick = nick,
                PostTs = nowTs,
                UpdateTs = nowTs,
                Type = SNSPost.TYPE_NORMAL,
                State = SNSPost.STATE_NORMAL
            };
            var postCol = SNSDb.GetCollection<SNSPost>("SNSPost");
            await postCol.InsertOneAsync(newPost);
            return SNSPostToJsonObject(newPost, newPost.Type);
        }

        [HttpGet("ReceivedLikes")]
        public async Task<IEnumerable<object>> GetReceivedLikes(long ts,int cnt)
        {
            var likeCol = SNSDb.GetCollection<SNSPostLike>("SNSPostLike");
            var likes = await likeCol.Find(lc=>lc.SNSPostUserId == UserObjectId && lc.Ts < ts).SortByDescending(l=>l.Ts).Limit(cnt).ToListAsync();
            var res = from l in likes select new 
            {
                ts = l.Ts,
                usrId = l.UserId.ToString(),
                nick = l.Nick,
                img = l.SNSPostImage
            };
            return res;
        }

        [HttpGet("MyComments")]
        public async Task<IEnumerable<object>> GetMyComments(long ts,int cnt)
        {
            var usrCol = SNSDb.GetCollection<SNSMemberProfile>("SNSMemberProfile");
            var member = await usrCol.Find(p => p.UserId == UserObjectId).CountAsync();
            if(member == 0)
            {
                Response.StatusCode = 404;
                return null;
            }
            var postCmtCol = SNSDb.GetCollection<SNSPostComment>("SNSPostComment");
            var cmts = await postCmtCol.Find(c => (c.AtUserId == UserObjectId || c.SNSPostPoster == UserObjectId || c.Poster == UserObjectId) && c.PostTs < ts).SortByDescending(c => c.PostTs).Limit(cnt).ToListAsync();
            var res = from c in cmts select SNSPostCommentToJsonObject(c,true);
            return res;
        }

        [HttpGet("PostComments")]
        public async Task<IEnumerable<object>> GetPostComments(string postId, long ts,int cnt = 30)
        {
            var postCmtCol = SNSDb.GetCollection<SNSPostComment>("SNSPostComment");
            var cmts = await postCmtCol.Find(c => c.PostId == new ObjectId(postId) && c.PostTs > ts).SortBy(c => c.PostTs).Limit(cnt).ToListAsync();
            var res = from c in cmts select SNSPostCommentToJsonObject(c);
            return res;
        }

        private object SNSPostCommentToJsonObject(SNSPostComment c,bool includeImage = false){
            return new
                      {
                          postId = c.PostId.ToString(),
                          cmt = c.Content,
                          ts = c.PostTs,
                          psterNk = c.PosterNick,
                          pster = c.Poster.ToString(),
                          atNick = c.AtNick,
                          img = includeImage ? c.SNSPostImage : null
                      };
        }

        [HttpPost("LikePost")]
        public async Task<object> LikePost(string postId,string nick = null)
        {
            if (await LikePost(postId,1,nick))
            {
                return new { msg = "SUCCESS" };
            }
            else
            {
                Response.StatusCode = 400;
                return null;
            }
        }

        private async Task<bool> LikePost(string postId, int likesCount,string nick)
        {
            var usrCol = SNSDb.GetCollection<SNSMemberProfile>("SNSMemberProfile");
            var likeCol = SNSDb.GetCollection<SNSPostLike>("SNSPostLike");
            var postCol = SNSDb.GetCollection<SNSPost>("SNSPost");
            try
            {
                if(string.IsNullOrWhiteSpace(nick)){
                    nick = "SNSer";
                }
                var post = await postCol.Find(p => p.Id == new ObjectId(postId) && p.State > 0).FirstAsync();
                var nowTs = (long)DateTimeUtil.UnixTimeSpan.TotalMilliseconds;
                var opt = new FindOneAndUpdateOptions<SNSPostLike, SNSPostLike>();
                opt.ReturnDocument = ReturnDocument.Before;
                opt.IsUpsert = true;
                opt.Projection = new ProjectionDefinitionBuilder<SNSPostLike>().Include(l => l.Ts);
                var filter = new FilterDefinitionBuilder<SNSPostLike>().Where(l => l.PostId == post.Id && l.UserId == UserObjectId);

                var updateLike = new UpdateDefinitionBuilder<SNSPostLike>()
                                    .Set(f => f.Ts, nowTs)
                                    .Set(l => l.PostId, post.Id)
                                    .Set(l => l.SNSPostUserId,post.UserId)
                                    .Set(l => l.Nick,nick)
                                    .Set(l => l.SNSPostImage,post.Image)
                                    .Set(l => l.UserId, UserObjectId);
                var like = await likeCol.FindOneAndUpdateAsync(filter, updateLike, opt);
                if (like == null)
                {
                    var update = new UpdateDefinitionBuilder<SNSMemberProfile>().Inc(p => p.Likes, likesCount).Inc(p => p.NewLikes, likesCount);
                    var usrOpt = new FindOneAndUpdateOptions<SNSMemberProfile, SNSMemberProfile>
                    {
                        ReturnDocument = ReturnDocument.After,
                        Projection = new ProjectionDefinitionBuilder<SNSMemberProfile>().Include(p => p.Likes).Include(p => p.ProfileState).Include(p => p.UserId)
                    };
                    var usrFilter = new FilterDefinitionBuilder<SNSMemberProfile>().Where(f => f.UserId == post.UserId);
                    var usr = await usrCol.FindOneAndUpdateAsync(usrFilter, update, usrOpt);
                    
                    var updatePost = new UpdateDefinitionBuilder<SNSPost>().Set(p => p.UpdateTs, (long)DateTimeUtil.UnixTimeSpan.TotalMilliseconds).Inc(p => p.Likes, likesCount);
                    await postCol.UpdateOneAsync(p => p.Id == new ObjectId(postId), updatePost);
                    if(post.UserId != UserObjectId)
                    {
                        AppServiceProvider.GetActivityService().AddActivityBadge(SNSConfigCenter.ActivityId, post.UserId, 1);
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
        public async Task<object> NewPostComment(string postId, string comment,string senderNick = null,string atUser = null,string atNick = null)
        {
            var usrCol = SNSDb.GetCollection<SNSMemberProfile>("SNSMemberProfile");
            var cmtPoster = await usrCol.Find(p => p.UserId == UserObjectId && p.ProfileState == SNSMemberProfile.STATE_NORMAL).CountAsync();

            if(cmtPoster == 0)
            {
                Response.StatusCode = 403;
                return null;
            }

            if(string.IsNullOrWhiteSpace(senderNick))
            {
                senderNick = await this.AppServiceProvider.GetUserService().GetUserNickOfUserId(UserObjectId);
            }
        
            var nowTs = (long)DateTimeUtil.UnixTimeSpan.TotalMilliseconds;
            
            var postCol = SNSDb.GetCollection<SNSPost>("SNSPost");
            var postFilter = new FilterDefinitionBuilder<SNSPost>().Where(p => p.Id == new ObjectId(postId) && p.State > 0);
            var postUpdate = new UpdateDefinitionBuilder<SNSPost>().Set(p => p.UpdateTs, nowTs).Inc(p => p.Cmts, 1);
            var postOpt = new FindOneAndUpdateOptions<SNSPost, SNSPost>
            {
                ReturnDocument = ReturnDocument.After,
                Projection = new ProjectionDefinitionBuilder<SNSPost>().Include(p => p.Id).Include(p => p.UserId).Include(p=>p.Image)
            };
            var post = await postCol.FindOneAndUpdateAsync(postFilter, postUpdate ,postOpt);
            var newCmt = new SNSPostComment
            {
                Content = comment,
                Poster = UserObjectId,
                PosterNick = senderNick,
                PostId = post.Id,
                PostTs = nowTs,
                SNSPostPoster = post.UserId,
                SNSPostImage = post.Image,
            };

            var badgedUserId = ObjectId.Empty;
            var activityService = AppServiceProvider.GetActivityService();
            if(!string.IsNullOrWhiteSpace(atUser)){
                ObjectId atUserId;
                if(ObjectId.TryParse(atUser,out atUserId)){
                    try {
                        var updateAtMember = new UpdateDefinitionBuilder<SNSMemberProfile>().Inc(p=>p.NewCmts,1);
                        var updateAtMemberFilter = new FilterDefinitionBuilder<SNSMemberProfile>().Where(p=>p.UserId == atUserId);
                        var modified = await usrCol.UpdateOneAsync(updateAtMemberFilter,updateAtMember);
                        if(modified.ModifiedCount > 0){
                            newCmt.AtUserId = atUserId;
                            newCmt.AtNick = atNick;
                            if(atUserId != UserObjectId){
                                activityService.AddActivityBadge(SNSConfigCenter.ActivityId,atUserId,1);
                                badgedUserId = atUserId;
                            }
                        }
                    }catch(Exception){}
                }
            }

            var postCmtCol = SNSDb.GetCollection<SNSPostComment>("SNSPostComment");
            await postCmtCol.InsertOneAsync(newCmt);
            
            if(UserObjectId != post.UserId){
                var update = new UpdateDefinitionBuilder<SNSMemberProfile>().Inc(p => p.NewCmts, 1);
                await usrCol.UpdateOneAsync(x => x.UserId == post.UserId, update);
                if(post.UserId != badgedUserId){
                    activityService.AddActivityBadge(SNSConfigCenter.ActivityId, post.UserId, 1);
                }
            }
            return new
            {
                msg = "SUCCESS"
            };
        }
    }
}