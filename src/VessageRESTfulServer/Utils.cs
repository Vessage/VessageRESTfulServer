using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerControlService.Model;
using VessageRESTfulServer.Services;
using BahamutService;
using ServerControlService.Service;
using NLog.Config;
using NLog;
using BahamutCommon;
using BahamutService.Service;
using System.IO;
using Newtonsoft.Json.Serialization;
using DataLevelDefines;
using System.Threading.Tasks;
using ServerControlService;
using Newtonsoft.Json;
using System.Threading;
using System.Net;
using System.Text;
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