using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace VessageRESTfulServer.Services
{
    public class VessageService
    {
        protected IMongoClient Client { get; set; }
        public VessageService(IMongoClient Client)
        {
            this.Client = Client;
        }
    }

    public static class GetVessageServiceExtension
    {
        public static VessageService GetVessageService(this IServiceProvider provider)
        {
            return provider.GetService<VessageService>();
        }
    }
}
