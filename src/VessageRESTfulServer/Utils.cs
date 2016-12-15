using Newtonsoft.Json;
using MongoDB.Driver.GeoJsonObjectModel;
using Newtonsoft.Json.Linq;

namespace VessageRESTfulServer
{
    public class Utils
    {
        static public GeoJson2DGeographicCoordinates LocationStringToLocation(string location)
        {
            var loc = JsonConvert.DeserializeObject<JObject>(location);
            var longitude = (double)loc["long"];
            var latitude = (double)loc["lati"];
            var altitude = (double)loc["alti"];
            return new GeoJson2DGeographicCoordinates(longitude, latitude);
        }
    }
}