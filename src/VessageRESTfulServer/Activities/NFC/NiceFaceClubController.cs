﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using Newtonsoft.Json;
using BahamutCommon;
using Newtonsoft.Json.Linq;
using System.Net;
using VessageRESTfulServer.Services;
using VessageRESTfulServer.Controllers;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Configuration;
using System.IO;
using VessageRESTfulServer.Models;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Activities.NFC
{
    public class NiceFaceClubConfigCenter
    {
        private static Random random = new Random(DateTime.UtcNow.Millisecond);
        public static Random Random { get { return random; } }
        private static IConfiguration _nfcConfig;
        private static IConfiguration NFCConfig
        {
            get
            {
                if (_nfcConfig == null)
                {
                    var configRoot = Startup.ConfigRoot + Path.DirectorySeparatorChar;
                    _nfcConfig = new ConfigurationBuilder()
                        .AddJsonFile(string.Format("{0}nfc_tips.json", configRoot), true, true)
                        .AddJsonFile(string.Format("{0}nfc_config.json", configRoot), true, true)
                        .Build();
                }
                return _nfcConfig;
            }
        }
        public static string NFCAnnounce { get { return NFCConfig["NFCAnnounce"]; } }

        public static string NFCJoinRuleAnnounce { get { return NFCConfig["NFCJoinRuleAnnounce"]; } }

        public static float FaceTestMaxAddtion { get { return float.Parse(NFCConfig["FaceTestMaxAddtionScore"]); } }
        public static float NFCBaseFaceScore { get { return float.Parse(NFCConfig["NFCBaseFaceScore"]); } }
        public static string ActivityId { get { return NFCConfig["ActId"]; } }
        public static string TestResultSignKey { get { return NFCConfig["FaceTestSignPrivateKey"]; } }

        public static int BaseLikeJoinNFC { get { return int.Parse(NFCConfig["BaseLikeJoinNFC"]); } }

        public static bool IsFaceTestLogEnabled { get { return bool.Parse(NFCConfig["FaceTestLog"]); } }

        public static string NFCHelloMessage { get { return NFCConfig["NFCHello"]; } }

        public static string NFCNeedLikeJoinMessage { get { return NFCConfig["NFCNeedLikeJoinMessage"]; } }


        public static string NFCName { get { return "高颜值俱乐部"; } }

        public static string GetScoreString(float highScore)
        {
            var resultLevTips = NFCConfig.GetSection("FaceTestResultTips").GetChildren();
            foreach (var item in resultLevTips)
            {
                var start = float.Parse(item["levRange:s"]);
                var end = float.Parse(item["levRange:e"]);
                if (highScore <= end && highScore > start)
                {
                    var msgs = item.GetSection("tips").GetChildren();
                    return msgs.ElementAt(random.Next() % msgs.Count()).Value;
                }
            }
            var tips = NFCConfig.GetSection("FaceTestNoFaceTips").GetChildren();
            return tips.ElementAt(random.Next() % tips.Count()).Value;
        }

        public static string GetRandomPuzzlePassMessage()
        {
            var msgs = NFCConfig.GetSection("PuzzlePassMsgs").GetChildren();
            return msgs.ElementAt(random.Next() % msgs.Count()).Value;
        }

        public static string GetRandomPuzzleNotPassMessage()
        {
            var msgs = NFCConfig.GetSection("PuzzleNotPassMsgs").GetChildren();
            return msgs.ElementAt(random.Next() % msgs.Count()).Value;
        }
    }

    [Route("api/[controller]")]
    public partial class NiceFaceClubController : APIControllerBase
    {
        private static Random random = new Random(DateTime.UtcNow.Millisecond);
        private IMongoDatabase NiceFaceClubDb
        {
            get
            {
                var client = AppServiceProvider.GetSharedService().GetMongoDBClient();
                return client.GetDatabase("NiceFaceClub");
            }
        }


        [HttpGet("MyNiceFace")]
        public async Task<object> GetMyNiceFace(string location = null)
        {
            var collection = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            try
            {
                var profile = await collection.Find(p => p.UserId == UserObjectId).FirstAsync();
                if (profile.ProfileState == NFCMemberProfile.STATE_BLACK_LIST)
                {
                    Response.StatusCode = 500;
                    return null;
                }
                var update = new UpdateDefinitionBuilder<NFCMemberProfile>().Set(p => p.ActiveTime, DateTime.UtcNow);
                if (!string.IsNullOrWhiteSpace(location))
                {
                    var c = Utils.LocationStringToLocation(location);
                    update = update.Set(p => p.Location, c);
                }
                await collection.UpdateOneAsync(p => p.Id == profile.Id, update);
                return MemberProfileToJsonObject(profile);
            }
            catch (Exception)
            {
                var tmpProfile = new NFCMemberProfile
                {
                    UserId = UserObjectId,
                    FaceScore = 0,
                    Id = UserObjectId,
                    Nick = "VGer",
                    Sex = 0,
                    Likes = 0,
                    ProfileState = NFCMemberProfile.STATE_ANONYMOUS
                };
                return MemberProfileToJsonObject(tmpProfile);
            }
        }

        [HttpGet("Profiles")]
        public async Task<object> GetMemberProfile(string profileId)
        {
            var collection = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            try
            {
                var profile = await collection.Find(p => p.Id == new ObjectId(profileId)).FirstAsync();
                if (profile.ProfileState == NFCMemberProfile.STATE_BLACK_LIST)
                {
                    Response.StatusCode = 500;
                    return null;
                }
                return MemberProfileToJsonObject(profile);
            }
            catch (Exception)
            {
                Response.StatusCode = 404;
                return null;
            }
        }

        [HttpPut("MyProfileValues")]
        public async Task<object> UpdateMyMemberProfile(string nick = null, int sex = int.MaxValue)
        {
            var update = new UpdateDefinitionBuilder<NFCMemberProfile>().Set(p => p.ActiveTime, DateTime.UtcNow);
            if (!string.IsNullOrWhiteSpace(nick))
            {
                update = update.Set(p => p.Nick, nick);
            }
            if (sex != int.MaxValue)
            {
                update = update.Set(p => p.Sex, sex);
            }
            var collection = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var result = await collection.UpdateOneAsync(p => p.UserId == UserObjectId, update);
            if (result.ModifiedCount > 0)
            {
                return new { msg = "SUCCESS" };
            }
            else
            {
                Response.StatusCode = 304;
                return new { msg = "NOT_MODIFIED" };
            }
        }

        public object MemberProfileToJsonObject(NFCMemberProfile profile)
        {
            var isSelf = profile.UserId.ToString() == UserSessionData.UserId;
            return new
            {
                id = isSelf ? UserSessionData.UserId : profile.Id.ToString(),
                nick = profile.Nick,
                sex = profile.Sex,
                faceId = profile.FaceImageId,
                score = profile.FaceScore,
                mbAcpt = profile.ProfileState == NFCMemberProfile.STATE_VALIDATED,
                likes = profile.Likes,
                mbId = isSelf ? profile.Id.ToString() : null
            };
        }


        [HttpPost("Like")]
        public object LikeMember(string profileId)
        {
            Response.StatusCode = (int)HttpStatusCode.Gone;
            return null;
        }

        [HttpPost("Dislike")]
        public object DislikeMember(string profileId)
        {
            Response.StatusCode = (int)HttpStatusCode.Gone;
            return null;
        }

        [HttpGet("FaceScoreTest")]
        public async Task<object> FaceScoreTest(string imageUrl, float addition)
        {
            try
            {
                var userId = UserSessionData.UserId;
                var apiUrl = "http://kan.msxiaobing.com/Api/ImageAnalyze/Process?service=beauty";
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0");
                var time = (long)DateTimeUtil.UnixTimeSpan.TotalSeconds;

                var paras = new KeyValuePair<string, string>[] {
                    new KeyValuePair<string, string>("MsgId",IDUtil.GenerateLongId().ToString()),
                    new KeyValuePair<string, string>("CreateTime",time.ToString()),
                    new KeyValuePair<string, string>("Content[imageUrl]",imageUrl)
                };
                var content = new FormUrlEncodedContent(paras);
                var result = await client.PostAsync(apiUrl, content);

                var resultContent = await result.Content.ReadAsStringAsync();


#if DEBUG
                Console.WriteLine(resultContent);
#else
                if (NiceFaceClubConfigCenter.IsFaceTestLogEnabled)
                {
                    LogInfo(resultContent);
                }
#endif

                dynamic obj = JsonConvert.DeserializeObject(resultContent);
                var metadata = (JObject)obj.content.metadata;
                var fbrCnt = 0;
                try
                {
                    fbrCnt = (int)metadata["FBR_Cnt"];
                }
                catch (Exception)
                {
                }

                var highScore = 0.0f;
                var sumScore = 0.0f;

                for (int i = 0; i < fbrCnt; i++)
                {
                    var s = (float)metadata["FBR_Score" + i];
                    sumScore += s;
                    if (s > highScore)
                    {
                        highScore = s;
                    }
                }

                var avgScore = sumScore / fbrCnt;

                var resScore = AdjustScore(highScore, sumScore, avgScore, fbrCnt, addition);

                if (resScore >= 10f)
                {
                    resScore = 9.9f;
                }
                else
                {
                    resScore = ((int)(resScore * 10f)) / 10f;
                }

                var msg = NiceFaceClubConfigCenter.GetScoreString(resScore);

                return new
                {
                    rId = GenerateResultId(time, resScore, userId),
                    hs = resScore,
                    msg = msg,
                    ts = time
                };
            }
            catch (Exception)
            {
                Response.StatusCode = 400;
                return null;
            }
        }

        private static float AdjustScore(float highScore, float sumScore, float avgScore, int scoreCnt, float addition)
        {
            var maxAddtion = NiceFaceClubConfigCenter.FaceTestMaxAddtion;
            var a = addition > maxAddtion ? maxAddtion : addition;
            return avgScore + a;
        }

        [HttpPost("NiceFace")]
        public async Task<object> SetNiceFace(string testResultId, string imageId, long timeSpan, float score)
        {
            if (score < NiceFaceClubConfigCenter.NFCBaseFaceScore)
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return new { msg = "LESS_THAN_BASE_SCORE" };
            }
            if (TestResultId(timeSpan, score, UserSessionData.UserId, testResultId))
            {
                var user = await AppServiceProvider.GetUserService().GetUserOfUserId(UserObjectId);
                var collection = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
                var update = new UpdateDefinitionBuilder<NFCMemberProfile>()
                    .Set(p => p.FaceImageId, imageId)
                    .Set(p => p.FaceScore, score)
                    .Set(p => p.Nick, user.Nick)
                    .Set(p => p.Sex, user.Sex)
                    .Set(p => p.ActiveTime, DateTime.UtcNow);
                var result = await collection.UpdateOneAsync(p => p.UserId == UserObjectId, update);
                if (result.MatchedCount == 0)
                {
                    await NewNFCMemberProfile(collection, user, imageId, score);
                    var members = await collection.Find(f => f.ProfileState == NFCMemberProfile.STATE_VALIDATED).Project(p => p.UserId).ToListAsync();
                    await Startup.ServicesProvider.GetActivityService().SetActivityMiniBadgeOfUserIds(NiceFaceClubConfigCenter.ActivityId, members, true);
                }
                return new { msg = "SUCCESS" };
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return new { msg = "VALIDATE_SCORE_FAIL" };
            }
        }

        private async Task NewNFCMemberProfile(IMongoCollection<NFCMemberProfile> collection, VessageUser user, string imageId, float score)
        {
            var profile = new NFCMemberProfile
            {
                CreateTime = DateTime.UtcNow,
                FaceImageId = imageId,
                FaceScore = score,
                UserId = UserObjectId,
                Nick = user.Nick,
                Sex = user.Sex,
                Likes = 0,
                ProfileState = NFCMemberProfile.STATE_ANONYMOUS,
                ActiveTime = DateTime.UtcNow
            };
            await collection.InsertOneAsync(profile);
            var nowTs = (long)DateTimeUtil.UnixTimeSpan.TotalMilliseconds;
            var newPost = new NFCPost
            {
                Image = imageId,
                Cmts = 0,
                Likes = 0,
                PosterNick = user.Nick,
                State = NFCPost.STATE_NORMAL,
                Type = NFCPost.TYPE_NEW_MEMBER,
                MemberId = profile.Id,
                UserId = user.Id,
                PostTs = nowTs,
                UpdateTs = nowTs
            };
            var postCol = NiceFaceClubDb.GetCollection<NFCPost>("NFCPost");
            await postCol.InsertOneAsync(newPost);
            var newCmt = new NFCPostComment
            {
                Content = "新人报道",
                Poster = profile.Id,
                PosterNick = user.Nick,
                PostId = newPost.Id,
                PostTs = nowTs
            };
            var postCmtCol = NiceFaceClubDb.GetCollection<NFCPostComment>("NFCPostComment");
            await postCmtCol.InsertOneAsync(newCmt);
            await AppServiceProvider.GetActivityService().CreateActivityBadgeData(NiceFaceClubConfigCenter.ActivityId, UserObjectId);
        }

        private static bool TestResultId(long timeSpan, float score, string userId, string resultId)
        {
            return resultId == GenerateResultId(timeSpan, score, userId);
        }

        private static string GenerateResultId(long timeSpan, float score, string userId)
        {
            var scoreInt = (int)(score * 10f);
            var sign = StringUtil.Md5String(string.Format("{0}:{1}:{2}:{3}", userId, timeSpan, scoreInt, NiceFaceClubConfigCenter.TestResultSignKey));
            return sign;
        }
    }
}
