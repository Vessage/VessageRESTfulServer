using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerControlService.Model;
using VessageRESTfulServer.Services;
using MongoDB.Driver;
using BahamutService;
using ServerControlService.Service;
using ServiceStack.Redis;
using NLog.Config;
using NLog;
using BahamutCommon;
using BahamutService.Service;
using System.IO;

namespace VessageRESTfulServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseConfiguration(configuration)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }

    public class Startup
    {
        public static IHostingEnvironment ServerHostingEnvironment { get; private set; }

        public static IConfiguration Configuration { get; set; }
        public static IServiceProvider ServicesProvider { get; private set; }
        public static BahamutAppInstance BahamutAppInstance { get; private set; }

        public static string Appkey { get; private set; }
        public static string Appname { get; private set; }
        public static string Server { get; set; }
        public static string APIUrl { get; private set; }

        public static string AuthServerUrl { get { return Configuration["Data:AuthServer:url"]; } }
        public static string FileApiUrl { get { return Configuration["Data:FileServer:url"]; } }
        public static string VessageDBServer { get { return Configuration["Data:VessageDBServer:url"]; } }
        public static string ChicagoServerAddress { get { return Configuration["Data:ChicagoServer:host"]; } }
        public static int ChicagoServerPort { get { return int.Parse(Configuration["Data:ChicagoServer:port"]); } }

        public static IDictionary<string, string> ValidatedUsers { get; private set; }
        public static bool IsProduction
        {
            get
            {
                return ServerHostingEnvironment.IsProduction();
            }
        }

        public Startup(IHostingEnvironment env)
        {
            // Set up configuration sources.
            ValidatedUsers = new Dictionary<string, string>();
            ReadConfig(env);
            SetServerConfig();
        }

        private static void ReadConfig(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json",true,true);
            var configFile = "config_debug.json";
            ServerHostingEnvironment = env;
            if (env.IsProduction())
            {
                configFile = "/etc/bahamut/vege/vessage.json";
            }
            builder.AddJsonFile(configFile,true,true);
            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        private static void SetServerConfig()
        {
            Server = Configuration["Data:App:url"];
            Appkey = Configuration["Data:App:appkey"];
            Appname = Configuration["Data:App:appname"];
            APIUrl = Server + "/api";
        }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.

            var tokenServerUrl = Configuration["Data:TokenServer:url"].Replace("redis://", "");
            var TokenServerClientManager = new PooledRedisClientManager(tokenServerUrl);
            var serverControlUrl = Configuration["Data:ControlServiceServer:url"].Replace("redis://", "");
            var ControlServerServiceClientManager = new PooledRedisClientManager(serverControlUrl);
            services.AddSingleton(new ServerControlManagementService(ControlServerServiceClientManager));
            services.AddSingleton(new TokenService(TokenServerClientManager));

            services.AddMvc(config => {
                config.Filters.Add(new BahamutAspNetCommon.LogExceptionFilter());
            });

            //business services
            services.AddSingleton(new UserService(new MongoClient(MongoUrl.Create(VessageDBServer))));
            services.AddSingleton(new VessageService(new MongoClient(MongoUrl.Create(VessageDBServer))));
            services.AddSingleton(new SharedService(new MongoClient(MongoUrl.Create(VessageDBServer))));
            services.AddSingleton(new ActivityService(new MongoClient(MongoUrl.Create(VessageDBServer))));

            //pubsub manager
            var pubsubServerUrl = Configuration["Data:MessagePubSubServer:url"].Replace("redis://", "");
            var pbClientManager = new PooledRedisClientManager(pubsubServerUrl);

            var messageCacheServerUrl = Configuration["Data:MessageCacheServer:url"].Replace("redis://", "");
            var mcClientManager = new PooledRedisClientManager(messageCacheServerUrl);

            var pbService = new BahamutPubSubService(pbClientManager);
            services.AddSingleton(pbService);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            Startup.ServicesProvider = app.ApplicationServices;
            //Log
            var logConfig = new LoggingConfiguration();
            LoggerLoaderHelper.LoadLoggerToLoggingConfig(logConfig, Configuration, "Data:Log:fileLoggers");

            if (env.IsDevelopment())
            {
                LoggerLoaderHelper.AddConsoleLoggerToLogginConfig(logConfig);
            }
            LogManager.Configuration = logConfig;

            //Regist App Instance
            var serverMgrService = ServicesProvider.GetServerControlManagementService();
            var appInstance = new BahamutAppInstance()
            {
                Appkey = Appkey,
                InstanceServiceUrl = Configuration["Data:App:url"],
                Region = Configuration["Data:App:region"]
            };
            try
            {
                BahamutAppInstance = serverMgrService.RegistAppInstance(appInstance);
                var observer = serverMgrService.StartKeepAlive(BahamutAppInstance);
                observer.OnExpireError += KeepAliveObserver_OnExpireError;
                observer.OnExpireOnce += KeepAliveObserver_OnExpireOnce;
                LogManager.GetLogger("Main").Info("Bahamut App Instance:" + BahamutAppInstance.Id.ToString());
                LogManager.GetLogger("Main").Info("Keep Server Instance Alive To Server Controller Thread Started!");
            }
            catch (Exception ex)
            {
                LogManager.GetLogger("Main").Error(ex, "Unable To Regist App Instance");
            }
            
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            //Authentication
            var openRoutes = new string[]
            {
                "/Tokens",
                "/NewUsers"
            };
            app.UseMiddleware<BahamutAspNetCommon.TokenAuthentication>(Appkey, ServicesProvider.GetTokenService(), openRoutes);

#if OPEN_HTTPS
            //HTTPS
            var certPath = "cert.pfx";
            var signingCertificate = new X509Certificate2(certPath, "test");
            app.UseKestrelHttps(signingCertificate);
#endif

            //Route
            app.UseStaticFiles();
            app.UseMvc();

            //Startup
            LogManager.GetLogger("Main").Info("VessageRESTful Server Started!");
        }

        private void KeepAliveObserver_OnExpireOnce(object sender, KeepAliveObserverEventArgs e)
        {

        }

        private void KeepAliveObserver_OnExpireError(object sender, KeepAliveObserverEventArgs e)
        {
            LogManager.GetLogger("Main").Error(string.Format("Expire Server Error.Instance:{0}", e.Instance.Id), e);
            var serverMgrService = ServicesProvider.GetServerControlManagementService();
            BahamutAppInstance.OnlineUsers = ValidatedUsers.Count;
            serverMgrService.ReActiveAppInstance(BahamutAppInstance);
        }
        
    }

    public static class IGetBahamutServiceExtension
    {
        public static ServerControlManagementService GetServerControlManagementService(this IServiceProvider provider)
        {
            return provider.GetService<ServerControlManagementService>();
        }

        public static TokenService GetTokenService(this IServiceProvider provider)
        {
            return provider.GetService<TokenService>();
        }

        public static BahamutPubSubService GetBahamutPubSubService(this IServiceProvider provider)
        {
            return provider.GetService<BahamutPubSubService>();
        }
    }
}
