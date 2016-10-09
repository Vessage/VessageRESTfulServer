using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VessageRESTfulServer.Activities.NFC
{

    public class NFCMemberProfile
    {
        public const int STATE_BLACK_LIST = -100;
        public const int STATE_VALIDATING = 1;
        public const int STATE_VALIDATED = 2;
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
        public int ProfileState { get; set; }

        //Post State
        public int Likes { get; set; }
        public int NewLikes { get; set; }
        public int NewCmts { get; set; }

        //public bool InBlackList { get; set; } //Use ProfileState Instead
    }

    public class NFCPost
    {
        public const int STATE_REMOVED = -1;
        public const int STATE_DELETED = -2;

        public const int STATE_NORMAL = 1;

        public const int TYPE_NORMAL = 0;
        public const int TYPE_NEW_MEMBER = 1;
        public const int TYPE_MY_POST = 2;

        public ObjectId Id { get; set; }
        public ObjectId MemberId { get; set; }
        public ObjectId UserId { get; set; }
        public string Image { get; set; }

        public long PostTs { get; set; }

        public long UpdateTs { get; set; }

        public int Likes { get; set; }
        public int Cmts { get; set; }
        public string PosterNick { get; set; }
        public int Type { get; set; }
        public int State { get; set; }
    }

    public class NFCPostLike
    {
        public ObjectId Id { get; set; }
        public ObjectId PostId { get; set; }
        public ObjectId UserId { get; set; }
        public long Ts { get; set; }
    }

    public class NFCPostComment
    {
        public ObjectId Id { get; set; }
        public ObjectId PostId { get; set; }
        public string Content { get; set; }
        public long PostTs { get; set; }
        public string PosterNick { get; set; }
        public ObjectId Poster { get; set; }
    }

}
