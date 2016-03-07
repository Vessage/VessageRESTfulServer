using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VessageRESTfulServer.Models;

namespace VessageRESTfulServer.Services
{
    public class VessageService
    {
        protected IMongoClient Client { get; set; }
        public VessageService(IMongoClient Client)
        {
            this.Client = Client;
        }

        internal Task<Vessage> SendVessage(Vessage vessage)
        {
            throw new NotImplementedException();
        }

        internal Task<bool> SetVessageRead(string vid)
        {
            throw new NotImplementedException();
        }

        internal Task<IEnumerable<Vessage>> GetNotReadMessageOfUser(string userId)
        {
            throw new NotImplementedException();
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
