using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace VessageRESTfulServer.Activities.NFC
{
    public partial class NiceFaceClubController
    {
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
                if (cans.Count == 0 || icans.Count == 0)
                {
                    continue;
                }
                var ca = (string)cans[random.Next() % cans.Count()];
                var ica = (string)icans[random.Next() % icans.Count()];
                var format = "{{\"qs\":\"{0}\",\"l\":\"{1}\",\"r\":\"{2}\"}}";
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
            NFCMemberProfile profile = null;
            try
            {
                profile = await collection.Find(p => p.UserId == UserObjectId).FirstAsync();
            }
            catch (Exception)
            {
            }

            if (profile == null || profile.FaceScore < NiceFaceClubConfigCenter.NFCBaseFaceScore || profile.ProfileState > 0)
            {
                return new object[0];
            }

            FilterDefinition<NFCMemberProfile> filter = null;
            if (preferSex > 0)
            {
                filter = new FilterDefinitionBuilder<NFCMemberProfile>().Where(p => profile.ProfileState > 0 && p.UserId != UserObjectId && p.Puzzles != null && p.Sex >= 0);
            }
            else if (preferSex < 0)
            {
                filter = new FilterDefinitionBuilder<NFCMemberProfile>().Where(p => profile.ProfileState > 0 && p.UserId != UserObjectId && p.Puzzles != null && p.Sex <= 0);
            }
            else
            {
                filter = new FilterDefinitionBuilder<NFCMemberProfile>().Where(p => profile.ProfileState > 0 && p.UserId != UserObjectId && p.Puzzles != null);
            }
            var res = await collection.Find(filter).SortByDescending(p => p.ActiveTime).Limit(20).ToListAsync();
            var objs = from r in res select MemberProfileToJsonObject(r);

            return objs.ToArray();
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


        [HttpPost("Puzzle")]
        public async Task<object> GuessPuzzle(string profileId, string answer)
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

    }
}
