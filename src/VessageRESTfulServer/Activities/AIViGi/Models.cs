using MongoDB.Bson;
using System;

namespace VessageRESTfulServer.Activities.AIViGi
{
    class AISNSFocus
    {
        public const int STATE_CANCELED = -1;
        public const int STATE_NORMAL = 0;
        public const int NOTIFICATION_STATE_ON = 0;
        public const int NOTIFICATION_STATE_OFF = 1;

        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }
        public string UserNick { get; set; }
        public string UserAccount { get; set; }
        public ObjectId FocusedUserId { get; set; }
        public string FocusedNoteName { get; set; }

        public DateTime LastPostDate { get; set; }
        public DateTime FetchDate { get; set; }

        public bool Linked { get; set; }

        public DateTime CreatedTime { get; set; }
        public DateTime UpdatedTime { get; set; }

        public int State { get; set; }
        public int NotificationState { get; set; }
        public string NotificationInfo { get; set; }
    }

    class AIViGiProfile
    {
        public ObjectId Id { get; set; }
        public string AccountId { get; set; }
        public ObjectId UserId { get; set; }
        public string MasterName { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime UpdatedTime { get; set; }
    }

    class AISNSPost
    {
        public const int STATE_OBJECTION_REVIEWING = 999;
        public const int STATE_DELETED = -999;
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

        public int ObjectionTimes { get; set; }
    }

    class AISNSPostObjection
    {
        public ObjectId Id { get; set; }
        public ObjectId PostId { get; set; }
        public string ObjectionNote { get; set; }
        public ObjectId ObjectionUserId { get; set; }

        public DateTime CreatedTime { get; set; }
    }
    
    class AIMessage
    {
        public const int BODY_TYPE_TEXT = 0;
        public const int STATE_NORMAL = 0;
        public const int STATE_RECEIVED = -1;
        public ObjectId Id { get; set; }
        public ObjectId Sender { get; set; }
        public string SNoteName { get; set; } //Sender Note Name
        public string Receiver { get; set; }
        public DateTime SendTime { get; set; }
        public int BodyType { get; set; }
        public string Body { get; set; }

        public int State { get; set; }
    }
}