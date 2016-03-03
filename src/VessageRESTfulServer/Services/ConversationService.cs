using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace VessageRESTfulServer.Services
{
    public class ConversationService
    {
        protected IMongoClient Client { get; set; }
        public ConversationService(IMongoClient Client)
        {
            this.Client = Client;
        }
    }

    public static class GetConversationServiceExtension
    {
        public static ConversationService GetConversationService(this IServiceProvider provider)
        {
            return provider.GetService<ConversationService>();
        }
    }
}
