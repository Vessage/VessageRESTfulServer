using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VessageRESTfulServer.Activities.MNS
{
    public class MNSProfile
    {
        public const int STATE_BLACK_LIST = -100;
        public const int STATE_NORMAL = 1;
        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }
        
        public string Nick { get; set; }

        public string Avatar { get; set; }
        public DateTime ActiveTime { get; set; }

        public DateTime CreateTime { get; set; }

        public string MidNightAnnounce { get; set; }

        public GeoJson2DGeographicCoordinates Location { get; set; }

        public int ProfileState { get; set; }
    }
}
