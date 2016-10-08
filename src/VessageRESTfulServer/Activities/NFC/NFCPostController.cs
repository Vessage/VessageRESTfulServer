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
using Microsoft.Extensions.Configuration;
using System.IO;

namespace VessageRESTfulServer.Activities
{
    public class NFCPost
    {
        public ObjectId Id { get; set; }
        public ObjectId MemberId { get; set; }
        public string Image { get; set; }

        public DateTime PostTime { get; set; }

        public int Likes { get; set; }
        public string PosterNick { get; set; }
        public int Type { get; set; }
    }

    public class NFCPostLike
    {
        public ObjectId Id { get; set; }
        public ObjectId PostId { get; set; }
        public ObjectId UserId { get; set; }
        public DateTime Time { get; set; }
    }

    public class NFCPostComment
    {
        public ObjectId Id { get; set; }
        public ObjectId PostId { get; set; }
        public string Content { get; set; }
        public DateTime PostTime { get; set; }
        public string PosterNick { get; set; }
        public ObjectId Poster { get; set; }
    }

    [Route("api/NiceFaceClub")]
    public class NFCPostController : APIControllerBase
    {
        
    }
}