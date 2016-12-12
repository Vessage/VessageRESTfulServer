using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VessageRESTfulServer.Activities.SNS
{

    public class SNSMemberProfile
    {
        public const int STATE_BLACK_LIST = -100;
        public const int STATE_NORMAL = 1;

        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }

        public DateTime CreateTime { get; set; }
        public DateTime ActiveTime { get; set; }
        public GeoJson2DGeographicCoordinates Location { get; set; }
        public int ProfileState { get; set; }

        //Post State
        public int Likes { get; set; }
        public int NewLikes { get; set; }
        public int NewCmts { get; set; }

        public ObjectId[] FocusUserIds { get; set; }
        public ObjectId[] Followers { get; set; }

    }

    public class SNSPost
    {
        public const int STATE_REMOVED = -1;
        public const int STATE_DELETED = -2;

        public const int STATE_NORMAL = 1;

        public const int STATE_IN_USER_OBJECTION = 2;

        public const int TYPE_NORMAL = 0;
        public const int TYPE_MY_POST = 1;

        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }
        public string Image { get; set; }

        public long PostTs { get; set; }

        public long UpdateTs { get; set; }

        public int Likes { get; set; }
        public int Cmts { get; set; }
        public string PosterNick { get; set; }
        public int Type { get; set; }
        public int State { get; set; }

        public string Body { get; set; }

    }

    public class SNSPostLike
    {
        public ObjectId Id { get; set; }
        public ObjectId PostId { get; set; }
        public ObjectId UserId { get; set; } //Add Like User Id
        public long Ts { get; set; }

        public string Nick { get; set; } //Add Like User Nick
        public ObjectId SNSPostUserId { get; set; } //SNS Post UserId
        public string SNSPostImage { get; set; }
    }

    public class SNSPostComment
    {
        public ObjectId Id { get; set; }
        public ObjectId PostId { get; set; }
        public string Content { get; set; }
        public long PostTs { get; set; }
        public string PosterNick { get; set; }

        public ObjectId Poster { get; set; } // Comment Poster

        public ObjectId AtUserId { get; set; }
        public string AtNick { get; set; }

        public ObjectId SNSPostPoster { get; set; } //SNS Post Poster

        public string SNSPostImage { get; set; }
    }

}
