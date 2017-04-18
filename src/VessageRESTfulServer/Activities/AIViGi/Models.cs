using MongoDB.Bson;
using System;

namespace VessageRESTfulServer.Activities.AIViGi
{
    class AISNSFocus
    {
        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }
        public ObjectId FocusedUserId { get; set; }
        public string FocusedNoteName { get; set; }

        public DateTime LastPostDate { get; set; }

        public bool Linked { get; set; }

        public DateTime CreatedTime { get; set; }
        public DateTime UpdatedTime { get; set; }

    }

    class AIViGiProfile
    {
        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }
        public string MasterName { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime UpdatedTime { get; set; }
    }

    class AISNSPost
    {
        public const int STATE_DELETED = -2;
        public const int STATE_REMOVED = -1;
        public const int STATE_NORMAL = 0;

        public const int TYPE_NORMAL = 0;

        public const int BODY_TYPE_TEXT = 0;
        public const int BODY_TYPE_JSON = 1;

        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }

        public string Body { get; set; }

        public int BodyType { get; set; }

        public int State { get; set; }

        public int Type { get; set; }

        public DateTime CreatedTime { get; set; }
        public DateTime UpdatedTime { get; set; }
    }
}