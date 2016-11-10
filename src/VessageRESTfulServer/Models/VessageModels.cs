using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver.GeoJsonObjectModel;

namespace VessageRESTfulServer.Models
{
    public class ChatImageInfo
    {
        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }
        public string ImageFileId { get; set; }
        public string ImageType { get; set; }
    }

    public class VessageUser
    {
        public ObjectId Id { get; set; }
        public string AccountId { get; set; }
        public string Nick { get; set; }
        public string Mobile { get; set; }
        public string MainChatImage { get; set; }
        public string Avartar { get; set; }
        public DateTime CreateTime { get; set; }
        public int Sex { get; set; }
        public string Motto { get; set; }
        public DateTime ActiveTime { get; set; }
        public GeoJson2DGeographicCoordinates Location { get; set; }
    }

    public class Vessage
    {
        public const int TYPE_CHAT_VIDEO = 0;
        public const int TYPE_FACE_TEXT = 1;
        public const int TYPE_IMAGE = 2;
        public const int TYPE_LITTLE_VIDEO = 3;

        public ObjectId Id { get; set; }
        public ObjectId Sender { get; set; }
        public string FileId { get; set; }
        public DateTime SendTime { get; set; }
        public bool IsRead { get; set; }
        public bool Ready { get; set; }
        public string ExtraInfo { get; set; }
        public bool IsGroup { get; set; }
        public int TypeId { get; set; }
        public string Body { get; set; }
        public string GroupSender { get; set; }
    }

    public class VessageBox
    {
        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }
        public string ForMobile { get; set; }
        public Vessage[] Vessages { get; set; }
        public DateTime LastGetMessageTime { get; set; }
        public DateTime LastGotMessageTime { get; set; }
        public bool IsGroup { get; set; }
    }

    public class ChatGroup
    {
        public ObjectId Id { get; set; }
        public ObjectId[] Hosters { get; set; }
        public ObjectId[] Chatters { get; set; }
        public string InviteCode { get; set; }
        public string GroupName { get; set; }
    }
}
