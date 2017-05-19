using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BahamutCommon;
using MongoDB.Driver;
using MongoDB.Bson;
using VessageRESTfulServer.Services;
using VessageRESTfulServer.Controllers;
using BahamutService.Service;
using Newtonsoft.Json;

namespace VessageRESTfulServer.Activities.AIViGi
{

    [Route("api/[controller]")]
    public partial class AIViGiSNSController : APIControllerBase
    {
        public const int CHECK_POST_LIMIT_DAYS = -14;
        private IMongoDatabase AiViGiSNSDb
        {
            get
            {
                var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
                return client.GetDatabase("AIViGiSNS");
            }
        }

        [HttpPost("FocusUser")]
        public async Task<object> FocusNewUserAsync(string userId, string noteName)
        {
            if (string.IsNullOrWhiteSpace(noteName))
            {
                Response.StatusCode = 417;
                return null;
            }

            var col = AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus");
            var focusdUserId = new ObjectId(userId);
            var userOId = UserObjectId;
            var userAccount = UserSessionData.AccountId;
            var now = DateTime.UtcNow;

            var updateLinked = new UpdateDefinitionBuilder<AISNSFocus>()
            .Set(p => p.Linked, true)
            .Set(p => p.UpdatedTime, now);

            var linked = await col.UpdateOneAsync(f => f.FocusedUserId == userOId && f.UserId == focusdUserId, updateLinked);

            var isLinked = linked.MatchedCount > 0;

            var update = new UpdateDefinitionBuilder<AISNSFocus>()
            .Set(p => p.UpdatedTime, now)
            .Set(p => p.FocusedNoteName, noteName)
            .Set(p => p.FocusedUserId, focusdUserId)
            .Set(p => p.Linked, isLinked)
            .Set(P => P.State, AISNSFocus.STATE_NORMAL);

            var r = await col.UpdateOneAsync(f => f.UserId == userOId && f.FocusedUserId == focusdUserId, update);
            if (r.MatchedCount == 0)
            {
                var nick = await AppServiceProvider.GetUserService().GetUserNickOfUserId(userOId);
                var newFocus = new AISNSFocus
                {
                    UserId = userOId,
                    UserAccount = userAccount,
                    UserNick = string.IsNullOrWhiteSpace(nick) ? userAccount : nick,
                    FocusedNoteName = noteName,
                    FocusedUserId = focusdUserId,
                    UpdatedTime = now,
                    CreatedTime = now,
                    FetchDate = DateTime.MinValue,
                    Linked = isLinked,
                    State = AISNSFocus.STATE_NORMAL,
                    LastPostDate = now,
                    NotificationState = AISNSFocus.NOTIFICATION_STATE_ON
                };
                await col.InsertOneAsync(newFocus);
                var format = isLinked ? "{0}关注了你，你可以用语音查看谁关注了你。" : "{0}关注了你，现在你们互相关注了对方。";
                var msg = String.Format(format, nick);
                var notification = new BahamutPublishModel
                {
                    NotifyInfo = JsonConvert.SerializeObject(new
                    {
                        BuilderId = 0,
                        AfterOpen = "go_custom",
                        Custom = "ViGiAddFocus",
                        Text = UserSessionData.UserId,
                        LocKey = msg,
                    }, Formatting.None),
                    NotifyType = "ViGiAddFocus",
                    ToUser = userId
                };
                AppServiceProvider.GetBahamutPubSubService().PublishVegeNotifyMessage(notification);
            }

            return new
            {
                usrId = userId,
                name = noteName,
                linked = isLinked,
                uts = DateTimeUtil.UnixTimeSpanOfDateTimeMs(now)
            };
        }

        [HttpPut("UnfocusUser")]
        public async Task<object> UnfocusUserAsync(string userId)
        {
            var col = AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus");
            var update = new UpdateDefinitionBuilder<AISNSFocus>().Set(p => p.State, AISNSFocus.STATE_CANCELED);
            var res = await col.UpdateOneAsync(f => f.UserId == UserObjectId && f.FocusedUserId == new ObjectId(userId), update);
            return new
            {
                code = res.MatchedCount > 0 ? 200 : 404,
                msg = res.MatchedCount > 0 ? "SUCCESS" : "NOT_FOUND"
            };
        }

        [HttpGet("FocusedUsers")]
        public async Task<IEnumerable<object>> GetFocusedUsersAsync(long lastTs = 0)
        {
            var col = AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus");
            var lastDate = DateTimeUtil.UnixTimeSpanZeroDate().AddMilliseconds(lastTs);
            var list = await col.Find(f => f.UserId == UserObjectId && f.State >= 0 && f.UpdatedTime > lastDate).Project(f => new
            {
                usrId = f.FocusedUserId.ToString(),
                name = f.FocusedNoteName,
                linked = f.Linked,
                uts = DateTimeUtil.UnixTimeSpanOfDateTimeMs(f.UpdatedTime),
                notifyState = f.NotificationState,
                notifyInfo = f.NotificationInfo
            }).ToListAsync();
            return list;
        }

        [HttpGet("FocusMeUsers")]
        public async Task<IEnumerable<object>> GetFollowersAsync()
        {
            var col = AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus");
            var list = await col.Find(f => f.FocusedUserId == UserObjectId && f.State >= 0).Project(f => new
            {
                usrId = f.UserId.ToString(),
                name = f.UserNick,
                linked = f.Linked,
                uts = DateTimeUtil.UnixTimeSpanOfDateTimeMs(f.UpdatedTime)
            }).ToListAsync();
            return list;
        }

        [HttpGet("UsersPosts")]
        public async Task<IEnumerable<object>> GetUsersPostsAsync(string userIds)
        {
            var userIdArr = from u in userIds.Split(new char[] { ',', '#' }) select new ObjectId(u);
            var col = AiViGiSNSDb.GetCollection<AISNSPost>("AISNSPost");

            var f1 = new FilterDefinitionBuilder<AISNSFocus>().Eq(f => f.UserId, UserObjectId);
            var f2 = new FilterDefinitionBuilder<AISNSFocus>().Gte(f => f.State, 0);
            var f3 = new FilterDefinitionBuilder<AISNSFocus>().In(f => f.FocusedUserId, userIdArr);

            var focusedUserIdArr = await AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus").Find(f1 & f2 & f3).Project(p => p.FocusedUserId).ToListAsync();

            var limitDate = DateTime.UtcNow.AddDays(CHECK_POST_LIMIT_DAYS);
            if (userIdArr.Contains(UserObjectId))
            {
                focusedUserIdArr.Add(UserObjectId);
                if (userIdArr.Count() == 1)
                {
                    limitDate = DateTime.MinValue;
                }
            }

            var containFilter = new FilterDefinitionBuilder<AISNSPost>().In(f => f.UserId, focusedUserIdArr);
            var stateFilter = new FilterDefinitionBuilder<AISNSPost>().Gte(f => f.State, AISNSPost.STATE_NORMAL);
            var dateFilter = new FilterDefinitionBuilder<AISNSPost>().Gte(f => f.CreatedTime, limitDate);
            var filter = stateFilter & dateFilter & containFilter;
            var posts = await col.Find(filter).SortByDescending(f => f.UpdatedTime).ToListAsync();
            return from p in posts select PostToJsonObject(p);
        }

        private object PostToJsonObject(AISNSPost post)
        {
            return new
            {
                pid = post.Id.ToString(),
                usrId = post.UserId.ToString(),
                body = post.Body,
                bdt = post.BodyType,
                t = post.Type,
                st = post.State,
                cts = DateTimeUtil.UnixTimeSpanOfDateTime(post.CreatedTime).TotalMilliseconds,
                uts = DateTimeUtil.UnixTimeSpanOfDateTime(post.UpdatedTime).TotalMilliseconds,
            };
        }

        [HttpGet("AllFocusedPosts")]
        public async Task<IEnumerable<object>> GetAllPosts()
        {
            var col = AiViGiSNSDb.GetCollection<AISNSPost>("AISNSPost");
            var userIdArr = await AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus").Find(f => f.UserId == UserObjectId).Project(p => p.FocusedUserId).ToListAsync();
            var containFilter = new FilterDefinitionBuilder<AISNSPost>().In(f => f.UserId, userIdArr);
            var stateFilter = new FilterDefinitionBuilder<AISNSPost>().Eq(f => f.State, AISNSPost.STATE_NORMAL);
            var dateFilter = new FilterDefinitionBuilder<AISNSPost>().Gte(f => f.CreatedTime, DateTime.UtcNow.AddDays(CHECK_POST_LIMIT_DAYS));
            var filter = stateFilter & dateFilter & containFilter;
            var posts = await col.Find(filter).SortByDescending(f => f.UpdatedTime).ToListAsync();
            return from p in posts select PostToJsonObject(p);
        }

        [HttpGet("RecentlyPostUsers")]
        public async Task<IEnumerable<object>> GetRecentlyPostUsers()
        {
            var col = AiViGiSNSDb.GetCollection<AISNSPost>("AISNSPost");

            var limitDate = DateTime.UtcNow.AddDays(CHECK_POST_LIMIT_DAYS);
            var focusedUserIdNames = await AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus")
            .Find(f => f.UserId == UserObjectId && f.State >= 0 && f.LastPostDate >= limitDate)
            .SortByDescending(f => f.LastPostDate).ThenBy(f => f.Linked)
            .Project(p => new
            {
                id = p.FocusedUserId.ToString(),
                name = p.FocusedNoteName,
                lpts = DateTimeUtil.UnixTimeSpanOfDateTimeMs(p.LastPostDate),
                lfts = DateTimeUtil.UnixTimeSpanOfDateTimeMs(p.FetchDate)
            })
            .ToListAsync();
            return focusedUserIdNames;
        }

        [HttpPost("NewPost")]
        public async Task PostNewAsync(string body, int bodyType = AISNSPost.BODY_TYPE_TEXT, bool notify = true)
        {
            var userOId = UserObjectId;
            var post = new AISNSPost
            {
                UserId = userOId,
                Body = body,
                BodyType = bodyType,
                CreatedTime = DateTime.UtcNow,
                UpdatedTime = DateTime.UtcNow,
                Type = AISNSPost.TYPE_NORMAL,
                State = AISNSPost.STATE_NORMAL
            };
            var col = AiViGiSNSDb.GetCollection<AISNSPost>("AISNSPost");
            await col.InsertOneAsync(post);
            if (notify)
            {
                await Task.Run(async () =>
                  {
                      var update = new UpdateDefinitionBuilder<AISNSFocus>().Set(f => f.LastPostDate, DateTime.UtcNow);
                      var focusCol = AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus");
                      await focusCol.UpdateManyAsync(f => f.FocusedUserId == userOId, update);

                      var followers = await focusCol.Find(f => f.FocusedUserId == userOId && f.NotificationState == AISNSFocus.NOTIFICATION_STATE_ON).Project(p => new { userId = p.UserId, noteName = p.FocusedNoteName }).ToListAsync();

                      var format = "{0}发布了新动态，要查看ta最新的动态请对我说：‘看看{0}的动态’";
                      foreach (var follower in followers)
                      {
                          var msg = String.Format(format, follower.noteName);
                          var notification = new BahamutPublishModel
                          {
                              NotifyInfo = JsonConvert.SerializeObject(new
                              {
                                  BuilderId = 0,
                                  AfterOpen = "go_custom",
                                  Custom = "ViGiHasNewPost",
                                  LocKey = msg,
                              }, Formatting.None),
                              NotifyType = "ViGiHasNewPost",
                              ToUser = follower.userId.ToString()
                          };
                          AppServiceProvider.GetBahamutPubSubService().PublishVegeNotifyMessage(notification);
                      }
                  });
            }
        }

        [HttpPut("FocusNotificationState")]
        public async Task<object> UpdateFocusNotificationStateAsync(string userId, int state, string info = null)
        {
            var focusCol = AiViGiSNSDb.GetCollection<AISNSFocus>("AISNSFocus");
            var update = new UpdateDefinitionBuilder<AISNSFocus>().Set(p => p.NotificationState, state);
            if (info != null)
            {
                update = update.Set(p => p.NotificationInfo, info);
            }
            var res = await focusCol.UpdateOneAsync(f => f.UserId == UserObjectId && f.FocusedUserId == ObjectId.Parse(userId), update);
            return new
            {
                code = res.MatchedCount > 0 ? 200 : 404
            };
        }

        [HttpPut("ObjectionPost")]
        public async Task<object> ObjectionPostAsync(string postId, string msg = null)
        {
            var update = new UpdateDefinitionBuilder<AISNSPost>()
            .Set(f => f.State, AISNSPost.STATE_OBJECTION_REVIEWING)
            .Inc(f => f.ObjectionTimes, 1);
            var postOId = new ObjectId(postId);
            var res = await AiViGiSNSDb.GetCollection<AISNSPost>("AISNSPost").UpdateOneAsync(f => f.Id == postOId && f.State != AISNSPost.STATE_DELETED, update);
            if (res.ModifiedCount > 0)
            {
                await AiViGiSNSDb.GetCollection<AISNSPostObjection>("AISNSPostObjection").InsertOneAsync(new AISNSPostObjection
                {
                    PostId = postOId,
                    ObjectionNote = msg,
                    ObjectionUserId = UserObjectId,
                    CreatedTime = DateTime.UtcNow
                });
            }
            return new
            {
                code = res.MatchedCount > 0 ? 200 : 404,
                msg = res.MatchedCount > 0 ? "SUCCESS" : "NOT_FOUND"
            };
        }

        [HttpDelete("RemovePost")]
        public async Task<object> RemovePostAsync(string postId)
        {
            var update = new UpdateDefinitionBuilder<AISNSPost>().Set(f => f.State, AISNSPost.STATE_REMOVED).Set(f => f.UpdatedTime, DateTime.UtcNow);
            var res = await AiViGiSNSDb.GetCollection<AISNSPost>("AISNSPost").UpdateOneAsync(f => f.Id == new ObjectId(postId) && f.UserId == UserObjectId, update);
            return new
            {
                code = res.MatchedCount > 0 ? 200 : 404,
                msg = res.MatchedCount > 0 ? "SUCCESS" : "NOT_FOUND"
            };
        }
    }
}