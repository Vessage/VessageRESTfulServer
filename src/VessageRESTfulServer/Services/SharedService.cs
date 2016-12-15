using System;
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
