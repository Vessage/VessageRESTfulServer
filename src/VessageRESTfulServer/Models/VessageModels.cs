using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VessageRESTfulServer.Models
{
    public class VessageUser
    {
        public ObjectId Id { get; set; }
        public string AccountId { get; set; }
        public string Nick { get; set; }
        public string Mobile { get; set; }
        public string MainChatImage { get; set; }
        public string Avartar { get; set; }
        public DateTime CreateTime { get; set; }
    }

    public class VMessage
    {
        public ObjectId Id { get; set; }
        public ObjectId Sender { get; set; }
        public ObjectId ConversatinoId { get; set; }
        public string Video{ get; set; }
        public DateTime SendTime { get; set; }
        public bool IsRead { get; set; }
    }

    public class Conversation
    {
        public ObjectId Id { get; set; }

        public ObjectId UserId { get; set; }
        public ObjectId ChattingUserId { get; set; }
        public string ChattingUserMobile { get; set; }

        public string ForUserMobile { get; set; }

        public string NoteName { get; set; }
        public DateTime LastMessageDateTime { get; set; }
    }

    public class ConversationList
    {
        public ObjectId Id { get; set; }
        public string UserId { get; set; }
        public Conversation[] Conversations { get; set; }
    }
}
