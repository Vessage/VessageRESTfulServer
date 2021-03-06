﻿using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using System;

namespace VessageRESTfulServer.Activities.NFC
{

    public class NFCMemberProfile
    {
        public const int STATE_BLACK_LIST = -100;
        public const int STATE_ANONYMOUS = 0;
        //public const int STATE_VALIDATING = 1; //Deprecated
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

        public const int STATE_IN_USER_OBJECTION = 2;

        public const int TYPE_NORMAL = 0;
        public const int TYPE_NEW_MEMBER = 1;
        public const int TYPE_MY_POST = 2;

        public const int TYPE_NEW_MEMBER_VALIDATED = 3;

        public ObjectId Id { get; set; }
        public ObjectId MemberId { get; set; }
        public ObjectId UserId { get; set; }
        public string Image { get; set; }

        public long PostTs { get; set; }

        public long UpdateTs { get; set; }

        public int Likes { get; set; }
        public int Cmts { get; set; }
        public string PosterNick { get; set; }
        public string PostAvatar { get; set; }
        public int Type { get; set; }
        public int State { get; set; }
        public string Body { get; set; }
    }

    public class NFCPostLike
    {
        public ObjectId Id { get; set; }
        public ObjectId PostId { get; set; }
        public ObjectId UserId { get; set; } //Add Like User Id
        public long Ts { get; set; }

        public ObjectId MemberId{ get; set;} //Add Like MemberId
        public string Nick { get; set; } //Add Like User Nick
        public ObjectId NFCPostUserId { get; set; } //NFC Post UserId
        public string NFCPostImage { get; set; }
    }

    public class NFCPostComment
    {
        public ObjectId Id { get; set; }
        public ObjectId PostId { get; set; }
        public string Content { get; set; }
        public long PostTs { get; set; }
        public string PosterNick { get; set; }

        public string CmtAvatar { get; set; }

        public ObjectId Poster { get; set; } // Comment Poster

        public ObjectId AtMemberId { get; set; }
        public string AtNick { get; set; }

        public ObjectId NFCPostPoster { get; set; } //NFC Post Poster

        public string NFCPostImage { get; set; }
    }

}
