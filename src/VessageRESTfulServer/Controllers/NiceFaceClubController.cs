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

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{
    [Route("api/[controller]")]
    public class NiceFaceClubController : Controller
    {
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
                    new KeyValuePair<string, string>("MsgId",time.ToString() + "023"),
                    new KeyValuePair<string, string>("CreateTime",time.ToString()),
                    new KeyValuePair<string, string>("Content[imageUrl]",imageUrl)
                };
                var content = new FormUrlEncodedContent(paras);
                var result = await client.PostAsync(apiUrl, content);

                var resultContent = await result.Content.ReadAsStringAsync();
                dynamic obj = JsonConvert.DeserializeObject(resultContent);
                var metadata = (JObject)obj.content.metadata;
                var fbrCnt = (int)metadata["FBR_Cnt"];
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
                return new
                {
                    highScore = highScore,
                    msg = msg
                };
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
#if DEBUG
                Console.WriteLine(ex.Message);
#endif
                Response.StatusCode = 400;
                return null;
            }
            
        }
    }
}
