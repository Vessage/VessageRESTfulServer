using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VessageRESTfulServer.Models;

namespace VessageRESTfulServer.Services
{
    public class ConversationService
    {
        protected IMongoClient Client { get; set; }
        public ConversationService(IMongoClient Client)
        {
            this.Client = Client;
        }

        internal Task<IEnumerable<Conversation>> GetUserConversations(string userId)
        {
            throw new NotImplementedException();
        }

        internal Task<Conversation> AddConversation(Conversation conversation)
        {
            throw new NotImplementedException();
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
