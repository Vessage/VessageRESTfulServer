using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Configuration;
using System.IO;
using VessageRESTfulServer.Models;
using System.Threading;
using Newtonsoft.Json;

namespace VessageRESTfulServer.Services
{

    public class SubAccount
    {
        public const int STATE_BLACK = -999;
        public const int STATE_OFFLINE = -1;
        public const int STATE_NORMAL = 0;
        public ObjectId Id { get; set; }
        public string AccountId { get; set; }
        public string UserId { get; set; }
        public string Title { get; set; }
        public string Desc { get; set; }
        public int State { get; set; }

        public long UpdateTs { get; set; }
    }

    public class SubscriptionService
    {
        public static IConfiguration SubscriptionConfig { get; private set; }

        protected IMongoClient Client { get; set; }
        public IMongoDatabase Db { get { return Client.GetDatabase("SubAccount"); } }

        private Dictionary<string, SubAccount> _subscriptionAccounts = new Dictionary<string, SubAccount>();

        private long updateAccountTs = 0;

        public SubscriptionService(IMongoClient Client)
        {
            this.Client = Client;
            LoadConfig();
            StartLoadSubscriptionAccountsThread();
        }

        private void StartLoadSubscriptionAccountsThread()
        {
            new Thread(f =>
            {
                while (true)
                {
                    LoadSubscriptionAccounts();
                    Thread.Sleep(60000);
                }
            }).Start();
        }

        private void LoadConfig()
        {
            var configRoot = Startup.ConfigRoot + Path.DirectorySeparatorChar;
            SubscriptionConfig = new ConfigurationBuilder()
                .AddJsonFile(string.Format("{0}sub_account_msgs.json", configRoot), true, true)
                .Build();
        }

        private void LoadSubscriptionAccounts()
        {
            var accounts = Db.GetCollection<SubAccount>("SubAccount").Find(f => f.State >= 0 && f.UpdateTs > updateAccountTs).ToList();
            foreach (var ac in accounts)
            {
                _subscriptionAccounts[ac.UserId] = ac;
                if (ac.UpdateTs > updateAccountTs)
                {
                    updateAccountTs = ac.UpdateTs;
                }
            }
        }

        public SubAccount GetSubscriptionAccount(string userId)
        {
            try
            {
                return _subscriptionAccounts[userId];
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        public IEnumerable<SubAccount> GetSubscriptionAccounts()
        {
            return _subscriptionAccounts.Values;
        }

        public IEnumerable<Vessage> GetSubscriptedVessages(SubAccount subAccount)
        {
            var now = DateTime.UtcNow.AddSeconds(-1);
            List<Vessage> lst = new List<Vessage>();

            foreach (var vsg in SubscriptionConfig.GetSection("SubscriptedVessages").GetChildren())
            {
                var v = new Vessage()
                {
                    Id = ObjectId.GenerateNewId(),
                    IsRead = false,
                    Sender = ObjectId.Parse(subAccount.UserId),
                    Ready = true,
                    FileId = null,
                    SendTime = now,
                    ExtraInfo = null,
                    IsGroup = false,
                    TypeId = int.Parse(vsg["type"]),
                    Body = vsg["body"],
                    GroupSender = null
                };
                lst.Add(v);
                now = now.AddMilliseconds(100);
            }

            var v0 = new Vessage()
            {
                Id = ObjectId.GenerateNewId(),
                IsRead = false,
                Sender = ObjectId.Parse(subAccount.UserId),
                Ready = true,
                FileId = null,
                SendTime = now,
                ExtraInfo = null,
                IsGroup = false,
                TypeId = Vessage.TYPE_FACE_TEXT,
                Body = JsonConvert.SerializeObject(new { textMessage = subAccount.Desc }),
                GroupSender = null
            };
            lst.Add(v0);

            return lst;
        }
    }

    public static class GetSubscriptionServiceExtension
    {
        public static SubscriptionService GetSubscriptionService(this IServiceProvider provider)
        {
            return provider.GetService<SubscriptionService>();
        }
    }
}