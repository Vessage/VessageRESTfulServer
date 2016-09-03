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
        public GeoJson2DGeographicCoordinates Location { get; set; }
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
            new object[] { 0.0f,7.0f,new string[] { "抱歉，测试没有达到要求。分享给你的好友，说不定Ta能加入哦！","没有通过测试，邀请朋友过来看看身边多少人能加入吧" } },
            new object[] { 7.0f,7.9f,new string[] { "别泄气，换个角度再测一次说不定就能过哦，我们期待你的加入!","AI嘛，有时不准，换个角度再来测一次说不定就过了" } },
            new object[] { 7.9f,8.3f,new string[] { "你的颜值达到组织的要求，赶快进来，组织需要你！！", "恭喜你通过测试！俱乐部有很多帅哥美女在等着你哦~~" } },
            new object[] { 8.3f,9.0f,new string[] { "轻松通过测试,NFC欢迎你！赶快确认加入俱乐部吧~","测试通过，欢迎来到NFC！确认后立即加入俱乐部~"} },
            new object[] { 9.0f,9.6f,new string[] { "颜值要爆表啊，AI君看后久久不能平复...","AI君:我是不是看到明星了??!!!" } },
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
            var arr = puzzleArr.ToArray().Take(leastCnt);
            var resultBuilder = new StringBuilder("[");
            var separator = "";
            foreach (var obj in arr)
            {
                var qs = (string)obj["question"];
                var cans = (JArray)obj["correct"];
                var icans = (JArray)obj["incorrect"];
                var ca = (string)cans[random.Next() % cans.Count()];
                var ica = (string)icans[random.Next() % icans.Count()];
                var format = "\"qs\":\"{0}\",\"l\":\"{1}\",\"r\":\"{2}\"";
                resultBuilder.Append(separator);
                resultBuilder.Append("{");
                if (random.Next() % 2 == 0)
                {
                    resultBuilder.Append(string.Format(format, qs, ca, ica));
                }
                else
                {
                    resultBuilder.Append(string.Format(format, qs, ica, ca));
                }
                resultBuilder.Append("}");
                separator = ",";
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

                highScore *= 0.91f;

                highScore = ((int)highScore * 10f) / 10f;
                
                if (addition > FACE_SCORE_MAX_ADDTION)
                {
                    addition = FACE_SCORE_MAX_ADDTION;
                }
                highScore += addition;
                var msg = GetScoreString(highScore);

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
            if (score < NFC_BASE_FACE_SCORE)
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
            var sign = StringUtil.Md5String(string.Format("{0}:{1}:{2}:{3}", userId, timeSpan, scoreInt, TEST_RESULT_SK));
            return sign;
        }
    }
}
