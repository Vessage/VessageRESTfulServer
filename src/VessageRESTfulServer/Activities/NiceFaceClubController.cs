﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using BahamutCommon;
using Newtonsoft.Json.Linq;
using System.Net;
using VessageRESTfulServer.Services;
using VessageRESTfulServer.Controllers;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using Microsoft.Extensions.Configuration;
using System.IO;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Activities
{
    public class NFCMemberProfile
    {
        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }
        public string Nick { get; set; }
        public float FaceScore { get; set; }
        public string FaceImageId { get; set; }
        public string Puzzles { get; set; }
        public int Sex { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime ActiveTime { get; set; }
        public GeoJson2DGeographicCoordinates Location { get; set; }
    }

    public class NiceFaceClubConfigCenter
    {
        private static Random random = new Random(DateTime.Now.Millisecond);
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
                        .AddJsonFile(string.Format("{0}nfc_tips.json",configRoot), true, true)
                        .AddJsonFile(string.Format("{0}nfc_config.json", configRoot), true, true)
                        .Build();
                }
                return _nfcConfig;
            }
        }

        public static float FaceTestMaxAddtion { get { return float.Parse(NFCConfig["FaceTestMaxAddtionScore"]); } }
        public static float NFCBaseFaceScore { get { return float.Parse(NFCConfig["NFCBaseFaceScore"]); } }
        public static string ActivityId { get { return NFCConfig["ActId"]; } }
        public static string TestResultSignKey { get { return NFCConfig["FaceTestSignPrivateKey"]; } }

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
    public class NiceFaceClubController : APIControllerBase
    {
        private static Random random = new Random(DateTime.Now.Millisecond);
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
                var update = new UpdateDefinitionBuilder<NFCMemberProfile>().Set(p => p.ActiveTime, DateTime.UtcNow);
                if (!string.IsNullOrWhiteSpace(location))
                {
                    var c = LocationStringToLocation(location); 
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

                };
                return MemberProfileToJsonObject(tmpProfile);
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

        object MemberProfileToJsonObject(NFCMemberProfile profile)
        {
            var isSelf = profile.UserId.ToString() == UserSessionData.UserId;
            return new
            {
                id = isSelf ? UserSessionData.UserId : profile.Id.ToString(),
                nick = profile.Nick,
                sex = profile.Sex,
                faceId = profile.FaceImageId,
                score = profile.FaceScore,
                puzzles = isSelf ? profile.Puzzles : RandomPuzzleForVisitor(profile.Puzzles)
            };
        }

        static private string RandomPuzzleForVisitor(string puzzles)
        {
            dynamic memberPuzzle = JsonConvert.DeserializeObject(puzzles);
            int leastCnt = memberPuzzle.leastCnt;
            JArray puzzleArr = memberPuzzle.puzzles;
            var resultBuilder = new StringBuilder("[");
            var separator = "";
            var sum = 0;
            foreach (var obj in puzzleArr.ToArray())
            {
                if (sum == leastCnt)
                {
                    break;
                }
                var qs = (string)obj["question"];
                var cans = (JArray)obj["correct"];
                var icans = (JArray)obj["incorrect"];
                if (cans.Count == 0|| icans.Count == 0)
                {
                    continue;
                }
                var ca = (string)cans[random.Next() % cans.Count()];
                var ica = (string)icans[random.Next() % icans.Count()];
                var format = "{\"qs\":\"{0}\",\"l\":\"{1}\",\"r\":\"{2}\"}";
                resultBuilder.Append(separator);
                if (random.Next() % 2 == 0)
                {
                    resultBuilder.Append(string.Format(format, qs, ca, ica));
                }
                else
                {
                    resultBuilder.Append(string.Format(format, qs, ica, ca));
                }
                separator = ",";
                sum++;
            }
            resultBuilder.Append("]");
            return resultBuilder.ToString();
        }

        [HttpGet("NiceFaces")]
        public async Task<IEnumerable<object>> GetNiceFaces(int preferSex, string location = null)
        {
            //TODO:
            //var c = LocationStringToLocation(location);
            var collection = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            FilterDefinition<NFCMemberProfile> filter = null;
            if (preferSex > 0)
            {
                filter = new FilterDefinitionBuilder<NFCMemberProfile>().Where(p => p.UserId != UserObjectId && p.Puzzles != null && p.Sex >= 0);
            }
            else if (preferSex < 0)
            {
                filter = new FilterDefinitionBuilder<NFCMemberProfile>().Where(p => p.UserId != UserObjectId && p.Puzzles != null && p.Sex <= 0);
            }
            else
            {
                filter = new FilterDefinitionBuilder<NFCMemberProfile>().Where(p => p.UserId != UserObjectId && p.Puzzles != null);
            }
            var res = await collection.Find(filter).SortByDescending(p => p.ActiveTime).Limit(20).ToListAsync();
            var objs = from r in res select MemberProfileToJsonObject(r);

            return objs.ToArray();
        }

        static private GeoJson2DGeographicCoordinates LocationStringToLocation(string location)
        {
            var loc = JsonConvert.DeserializeObject<JObject>(location);
            var longitude = (double)loc["long"];
            var latitude = (double)loc["lati"];
            var altitude = (double)loc["alti"];
            return new GeoJson2DGeographicCoordinates(longitude, latitude);
        }

        [HttpPut("PuzzleAnswer")]
        public async Task<object> SetPuzzleAnswer(string puzzle)
        {
            if (string.IsNullOrWhiteSpace(puzzle))
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return new { msg = "FAIL" };
            }
            var collection = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var update = new UpdateDefinitionBuilder<NFCMemberProfile>().Set(p => p.Puzzles, puzzle);
            var result = await collection.UpdateOneAsync(p => p.UserId == UserObjectId, update);
            if (result.ModifiedCount > 0)
            {
                return new { msg = "SUCCESS" };
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                return new { msg = "FAIL" };
            }
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

        [HttpPost("Puzzle")]
        public async Task<object> GuessPuzzle(string profileId,string answer)
        {
            
            var collection = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var profile = await collection.Find(p => p.Id == new ObjectId(profileId)).FirstAsync();
            var psJson = profile.Puzzles;
            var answerArr = from a in (JArray)JsonConvert.DeserializeObject(answer) select (string)a;
            JObject puzzle = (JObject)JsonConvert.DeserializeObject(psJson);
            var pass = false;
            var puzzles = (JArray)puzzle["puzzles"];

            var correctAnswers = from p in puzzles from a in (JArray)p["correct"] select (string)a;
            if (correctAnswers.Count() > 0 && answerArr.Count() > 0)
            {
                var result = correctAnswers.Intersect(answerArr);
                if (result.Count() == answerArr.Count())
                {
                    pass = true;
                }
            }
            string nick = null;
            string msg = null;
            string userId = null;
            
            if (pass)
            {
                msg = NiceFaceClubConfigCenter.GetRandomPuzzlePassMessage();
                userId = profile.UserId.ToString();
                nick = profile.Nick;
            }
            else
            {
                msg = NiceFaceClubConfigCenter.GetRandomPuzzleNotPassMessage();
            }
            return new
            {
                id = profileId,
                pass = pass,
                userId = userId,
                msg = msg,
                nick = nick
            };
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
                
                for (int i = 0; i < fbrCnt; i++)
                {
                    var s = (float)metadata["FBR_Score" + i];
                    if (s > highScore)
                    {
                        highScore = s;
                    }
                }

                highScore = AdjustScore(highScore, addition);
                var msg = NiceFaceClubConfigCenter.GetScoreString(highScore);

                return new
                {
                    rId = GenerateResultId(time,highScore,userId),
                    hs = highScore,
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

        private static float AdjustScore(float highScore,float addition)
        {
            var maxAddtion = NiceFaceClubConfigCenter.FaceTestMaxAddtion;
            addition = addition > maxAddtion ? maxAddtion : addition;
            var random = NiceFaceClubConfigCenter.Random;
            var ad = addition * random.Next(66, 88) / 100f;
            highScore = highScore >= 8.6 ? 8.0f + (highScore - 8.6f) / (0.9f - ad) : 8.0f - 8.6f * (1f - highScore / (8.6f - ad));
            highScore += addition * 0.4f;
            highScore = ((int)(highScore * 10f)) / 10f;
            return highScore > 10f ? 10f : highScore < 3 ? 0f : highScore;
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
                    var profile = new NFCMemberProfile
                    {
                        CreateTime = DateTime.UtcNow,
                        FaceImageId = imageId,
                        FaceScore = score,
                        UserId = UserObjectId,
                        Nick = user.Nick,
                        Sex = user.Sex,
                        ActiveTime = DateTime.UtcNow
                    };
                    await collection.InsertOneAsync(profile);
                }
                return new { msg = "SUCCESS" };
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return new { msg = "VALIDATE_SCORE_FAIL" };
            }
        }

        private static bool TestResultId(long timeSpan,float score,string userId,string resultId)
        {
            return resultId == GenerateResultId(timeSpan, score, userId);
        }

        private static string GenerateResultId(long timeSpan, float score,string userId)
        {
            var scoreInt = (int)(score * 10);
            var sign = StringUtil.Md5String(string.Format("{0}:{1}:{2}:{3}", userId, timeSpan, scoreInt, NiceFaceClubConfigCenter.TestResultSignKey));
            return sign;
        }
    }
}