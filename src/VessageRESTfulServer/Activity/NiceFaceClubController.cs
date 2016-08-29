using System;
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

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Activity
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
        public GeoJson3DGeographicCoordinates Location { get; set; }
    }

    [Route("api/[controller]")]
    public class NiceFaceClubController : APIControllerBase
    {
        public const string ActivityId = "1002";
        private const string TEST_RESULT_SK = "NICE_FACE_WONDERFUL";
        private const float FACE_SCORE_MAX_ADDTION = 0.2f;
        private const float NFC_BASE_FACE_SCORE = 8.0f;
        private static Random random = new Random(DateTime.Now.Millisecond);
        private static readonly string[] PASS_MSGS = new string[] 
        {
            "哈哈，被你猜到了!",
            "终于有个人懂我了，交个朋友呗！",
            "你是真懂我还是蒙的，说？",
            "我们挺像的，聊聊吧！"
        };

        private static readonly string[] NOT_PASS_MSGS = new string[]
        {
            "哈哈，猜错咯",
            "唉，找个懂我的人怎么那么难呢...",
            "左划右划一个慢动作，但是你却不懂我",
            "我们没有共同话题，先撤了"
        };

        private static readonly object[][] FACE_TEST_MSGS = new object[][] 
        {
            new object[] { 0.0f,7.6f,new string[] { "抱歉，测试没有达到要求。分享给你的好友，说不定Ta能加入哦！","没有通过测试，邀请朋友过来看看身边多少人能加入吧" } },
            new object[] { 7.6f,8.0f,new string[] { "别泄气，换个角度再测一次说不定就能过哦，我们期待你的加入!","AI嘛，有时不准，再来测一次说不定就过了" } },
            new object[] { 8.0f,8.3f,new string[] { "你的颜值达到组织的要求，赶快进来，组织需要你", "恭喜你通过测试！俱乐部有很多帅哥美女在等着你哦~~" } },
            new object[] { 8.3f,9.0f,new string[] { "轻松通过测试,NFC欢迎你！","测试通过，欢迎加入NFC！"} },
            new object[] { 9.0f,9.6f,new string[] { "颜值要爆表啊，AI君看后久久不能平复..." } },
            new object[] { 9.6f,10.0f,new string[] { "请问你是谁？这颜值要逆天啊！！！","OMG，这世界竟然有如此高颜值的人，这个俱乐部为你而生","！！！！！！！！！(不知道说什么，怎么办！)" } }
        };

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
            var userService = AppServiceProvider.GetUserService();
            var collection = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var profile = await collection.Find(p => p.UserId == UserObjectId).FirstAsync();
            var user = await userService.GetUserOfUserId(UserObjectId);
            
            var update = new UpdateDefinitionBuilder<NFCMemberProfile>().Set(p => p.ActiveTime, DateTime.UtcNow);
            if (!string.IsNullOrWhiteSpace(location))
            {
                var loc = JsonConvert.DeserializeObject<JObject>(location);
                var longitude = (double)loc["long"];
                var latitude = (double)loc["lati"];
                var altitude = (double)loc["alti"];
                var c = new GeoJson3DGeographicCoordinates(longitude, latitude, altitude);
                update = update.Set(p => p.Location, c);
            }
            if (user.Nick != profile.Nick )
            {
                update = update.Set(p => p.Sex, user.Sex);
            }
            if (user.Sex != profile.Sex)
            {
                update = update.Set(p => p.Nick, user.Nick);
            }
            await collection.UpdateOneAsync(p => p.Id == profile.Id, update);
            return new
            {
                profileId = UserSessionData.UserId,
                nick = user.Nick,
                sex = user.Sex,
                faceImage = profile.FaceImageId,
                score = profile.FaceScore,
                puzzles = profile.Puzzles
            };
        }

        [HttpGet("NiceFaces")]
        public IEnumerable<object> GetNiceFaces(int preferSex)
        {
            //TODO:
            var collection = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var res = collection.AsQueryable().Where(p=>p.Sex * preferSex >= 0).OrderByDescending(p => p.CreateTime).Take(10).Select(p => p);
            
            var objs = from r in res
                       select new
                       {
                           profileId = r.Id.ToString(),
                           nick = r.Nick,
                           sex = r.Sex,
                           faceImage = r.FaceImageId,
                           score = r.FaceScore,
                           puzzles = r.Puzzles
                       };

            return objs.ToArray();
        }

        [HttpPut("PuzzleAnswer")]
        public async Task<object> SetPuzzleAnswer(string answer,string allAnswer)
        {
            var collection = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var update = new UpdateDefinitionBuilder<NFCMemberProfile>().Set(p => p.Puzzles, answer);
            var result = await collection.UpdateOneAsync(p => p.UserId == UserObjectId, update);
            return new { msg = "SUCCESS" };
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
            var answers = from a in answer.Split(new char[] { ',' }) where !string.IsNullOrWhiteSpace(a) select a;
            var collection = NiceFaceClubDb.GetCollection<NFCMemberProfile>("NFCMemberProfile");
            var profile = await collection.Find(p => p.UserId == UserObjectId).FirstAsync();
            var correctAnswer = profile.Puzzles;
            var pass = false;
            if (answers.Count() > 0)
            {
                pass = true;
                foreach (var a in answers)
                {
                    if (!correctAnswer.Contains(a))
                    {
                        pass = false;
                        break;
                    }
                }
            }

            string nick = null;
            string msg = null;
            string userId = null;
            
            if (pass)
            {
                msg = PASS_MSGS[random.Next() % PASS_MSGS.Count()];
                userId = profile.UserId.ToString();
                nick = profile.Nick;
            }
            else
            {
                msg = NOT_PASS_MSGS[random.Next() % NOT_PASS_MSGS.Count()];
            }
            return new
            {
                id = profileId,
                pass = pass,
                memberUserId = userId,
                msg = msg,
                memberNick = nick
            };
        }

        [HttpGet("FaceScoreTest")]
        public async Task<object> FaceScoreTest(string imageUrl, float addition)
        {
            try
            {
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

                if (addition > FACE_SCORE_MAX_ADDTION)
                {
                    addition = FACE_SCORE_MAX_ADDTION;
                }

                highScore += addition;

                var msg = GetScoreString(highScore);

                var resultId = GenerateResultId(time, highScore, UserSessionData.UserId);
                return new
                {
                    resultId = GenerateResultId(time,highScore,UserSessionData.UserId),
                    highScore = ((int)(highScore * 0.92 * 10)) / 10f,
                    msg = msg,
                    timeSpan = time
                };
            }
            catch (Exception)
            {
                Response.StatusCode = 400;
                return null;
            }
            
        }

        private static string GetScoreString(float highScore)
        {
            foreach (var item in FACE_TEST_MSGS)
            {
                var start = (float)item[0];
                var end = (float)item[1];
                if (highScore <= end && highScore > start)
                {
                    var msgs = (string[])item[2];
                    return msgs[random.Next() % msgs.Count()];
                }
            }
            return "我什么都看不见";
        }

        [HttpPost("NiceFace")]
        public async Task<object> SetNiceFace(string testResultId, string imageId, long timeSpan, float score)
        {
            if (score <= NFC_BASE_FACE_SCORE)
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
            return StringUtil.Md5String(string.Format("{0}:{1}:{2}:{3}", userId, timeSpan, score, TEST_RESULT_SK));
        }
    }
}
