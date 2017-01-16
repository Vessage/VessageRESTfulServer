using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using System;

namespace VessageRESTfulServer.Activities.PAP
{

    public class PaperAirplaneMessage
    {
        public ObjectId UserId { get; set; }
        public string Nick { get; set; }
        public string Avatar { get; set; }
        public DateTime CreateTime { get; set; }

        public string Content { get; set; }
        public GeoJson2DGeographicCoordinates Location { get; set; }
    }
    
    public class PaperAirplane
    {
        public const int STATE_FLYING = 1;
        public const int STATE_OWNER_KEEPING = 2;
        public const int STATE_DESTROIED = 3;

        public ObjectId Id { get; set; }
        public PaperAirplaneMessage[] Messages { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime UpdatedTime { get; set; }

        public int State { get; set; }

        public ObjectId Owner { get; set; }
    }
}
