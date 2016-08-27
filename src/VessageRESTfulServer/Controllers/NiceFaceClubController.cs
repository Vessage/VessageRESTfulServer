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

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{
    [Route("api/[controller]")]
    public class NiceFaceClubController : APIControllerBase
    {
        private const string TEST_RESULT_SK = "NICE_FACE_WONDERFUL";

        [HttpGet("MyNiceFace")]
        public async Task<object> GetMyNiceFace()
        {
            //TODO:
            var user = await AppServiceProvider.GetUserService().GetUserOfUserId(UserObjectId);
            return new
            {
                profileId = UserSessionData.UserId,
                nick = user.Nick,
                sex = user.Sex,
                faceImage = user.MainChatImage,
                score = 8.0,
                puzzles = "🎾,🏊;🍊,🍎;🐶,😺"
            };
        }

        [HttpGet("NiceFaces")]
        public async Task<IEnumerable<object>> GetNiceFaces()
        {
            //TODO:
            var user = await AppServiceProvider.GetUserService().GetUserOfUserId(UserObjectId);
            return new object[]
            {
                new
                {
                    profileId = IDUtil.GenerateLongId().ToString(),
                    nick = user.Nick,
                    sex = user.Sex,
                    faceImage = user.MainChatImage,
                    score = 8.0 + new Random(DateTime.Now.Millisecond).Next(0,20) / 10.0,
                    puzzles = "🎾,🏊;🍊,🍎;🐶,😺"
                },
                new
                {
                    profileId = IDUtil.GenerateLongId().ToString(),
                    nick = user.Nick,
                    sex = user.Sex,
                    faceImage = user.MainChatImage,
                    score = 8.0 + new Random(DateTime.Now.Millisecond).Next(0,20) / 10.0,
                    puzzles = "🍌,🍊;🍡,🍣;🎳,🎾"
                }
            };
        }

        [HttpPut("PuzzleAnswer")]
        public object SetPuzzleAnswer(string answer)
        {
            return null;
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
            //TODO:
            var user = await AppServiceProvider.GetUserService().GetUserOfAccountId("147264");
            var pass = (new Random(DateTime.Now.Millisecond).Next(0,10) % 2) == 0;
            return new
            {
                id = profileId,
                pass = pass,
                memberUserId = pass ? user.Id.ToString() : null,
                msg = "",
                memberNick = "Nick"
            };
        }

        [HttpGet("FaceScoreTest")]
        public async Task<object> FaceScoreTest(string imageUrl)
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
                var msg = (string)obj.content.text;
                for (int i = 0; i < fbrCnt; i++)
                {
                    var s = (float)metadata["FBR_Score" + i];
                    if (s > highScore)
                    {
                        highScore = s;
                    }
                }

                var resultId = GenernateResultId(time, highScore, UserSessionData.UserId);
                return new
                {
                    resultId = GenernateResultId(time,highScore,UserSessionData.UserId),
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

        [HttpPost("NiceFace")]
        public object SetNiceFace(string testResultId,string imageId,long timeSpan,float score)
        {
            if (TestResultId(timeSpan,score,UserSessionData.UserId,testResultId))
            {

                return true;
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return null;
            }
        }

        private static bool TestResultId(long timeSpan,float score,string userId,string resultId)
        {
            return resultId == GenernateResultId(timeSpan, score, userId);
        }

        private static string GenernateResultId(long timeSpan, float score,string userId)
        {
            return StringUtil.Md5String(string.Format("{0}:{1}:{2}:{3}", userId, timeSpan, score, TEST_RESULT_SK));
        }
    }
}
