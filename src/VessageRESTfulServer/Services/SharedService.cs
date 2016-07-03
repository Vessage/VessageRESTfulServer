using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace VessageRESTfulServer.Services
{

    public class SharedService
    {
        protected IMongoClient Client { get; set; }
        public SharedService(IMongoClient Client)
        {
            this.Client = Client;
        }

        public IMongoClient GetMongoDBClient()
        {
            return Client;
        }
    }
    
    public static class SharedServiceExtensions
    {
        public static SharedService GetSharedService(this IServiceProvider provider)
        {
            return provider.GetService<SharedService>();
        }
    }
}
