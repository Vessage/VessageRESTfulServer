using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
    }

    public class Vessage
    {
        public ObjectId Id { get; set; }
        public ObjectId Sender { get; set; }
        public string Video{ get; set; }
        public DateTime SendTime { get; set; }
        public bool IsRead { get; set; }
        public bool VideoReady { get; set; }
        public string ExtraInfo { get; set; }
        public bool IsGroup { get; set; }
        public int TypeId { get; set; }
        public string Body { get; set; }
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
