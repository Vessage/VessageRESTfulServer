﻿using System;
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
using Newtonsoft.Json.Serialization;
using DataLevelDefines;

namespace VessageRESTfulServer
{
    public class Program
    {
        public static IConfiguration ArgsConfig { get; private set; }
        public static void Main(string[] args)
        {
            ArgsConfig = new ConfigurationBuilder().AddCommandLine(args).Build();
            var configFile = ArgsConfig["config"];
            if (string.IsNullOrEmpty(configFile))
            {
                Console.WriteLine("No Config File");
            }
            else
            {
                var hostBuilder = new WebHostBuilder()
                .UseKestrel()
                .UseConfiguration(ArgsConfig)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>();

                var appConfig = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile(configFile).Build();
                var urls = appConfig["Data:App:urls"].Split(new char[] { ';', ',', ' ' });
                hostBuilder.UseUrls(urls);
                hostBuilder.Build().Run();
            }
        }
    }

    public class Startup
    {
        public static IHostingEnvironment ServerHostingEnvironment { get; private set; }

        public static IConfiguration Configuration { get; set; }
        public static IServiceProvider ServicesProvider { get; private set; }
        public static BahamutAppInstance BahamutAppInstance { get; private set; }

        public static string Appkey { get { return Configuration["Data:App:appkey"]; } }
        public static string Appname { get { return Configuration["Data:App:appname"]; } }
        public static string RegistNewUserApiUrl { get { return Configuration["Data:RegistNewUserApiUrl"]; } }
        public static string ServiceApiUrl { get { return Configuration["Data:ServiceApiUrl"]; } }
        public static string ServiceApiUrlRoute { get { return ServiceApiUrl + "/api"; } }

        public static string AuthServerUrl { get { return Configuration["Data:AuthServer:url"]; } }
        public static string FileApiUrl { get { return Configuration["Data:FileServer:url"]; } }
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
        }

        private static void ReadConfig(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath);
            var configFile = Program.ArgsConfig["config"];
            var baseConfig = builder.AddJsonFile(configFile, true, true).Build();
            var logConfig = baseConfig["Data:LogConfig"];
            builder.AddJsonFile(configFile, true, true);
            builder.AddJsonFile(logConfig, true, true);
            ServerHostingEnvironment = env;
            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            var TokenServerClientManager = DBClientManagerBuilder.GenerateRedisClientManager(Configuration.GetSection("Data:TokenServer"));
            var ControlServerServiceClientManager = DBClientManagerBuilder.GenerateRedisClientManager(Configuration.GetSection("Data:ControlServiceServer"));
            services.AddSingleton(new ServerControlManagementService(ControlServerServiceClientManager));
            services.AddSingleton(new TokenService(TokenServerClientManager));

            services.AddMvc(config => {
                config.Filters.Add(new BahamutAspNetCommon.LogExceptionFilter());
            }).AddJsonOptions(op =>
            {
                op.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            });

            //business services
            var mongoClient = DBClientManagerBuilder.GeneratePoolMongodbClient(Configuration.GetSection("Data:VessageDBServer"));
            services.AddSingleton(new UserService(mongoClient));
            services.AddSingleton(new VessageService(mongoClient));
            services.AddSingleton(new SharedService(mongoClient));
            services.AddSingleton(new ActivityService(mongoClient));
            services.AddSingleton(new GroupChatService(mongoClient));

            //pubsub manager
            var pbClientManager = DBClientManagerBuilder.GenerateRedisClientManager(Configuration.GetSection("Data:MessagePubSubServer"));

            var pbService = new BahamutPubSubService(pbClientManager);
            services.AddSingleton(pbService);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            Startup.ServicesProvider = app.ApplicationServices;
            //Log
            var logConfig = new LoggingConfiguration();
            LoggerLoaderHelper.LoadLoggerToLoggingConfig(logConfig, Configuration, "Logger:fileLoggers");

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
                InstanceServiceUrl = ServiceApiUrl,
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

            //Authentication
            var openRoutes = new string[]
            {
                "/Tokens",
                "/NewUsers"
            };
            app.UseMiddleware<BahamutAspNetCommon.TokenAuthentication>(Appkey, ServicesProvider.GetTokenService(), openRoutes);

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

    public static class IPubSubServiceExtension
    {
        public static void PublishVegeNotifyMessage(this BahamutPubSubService service,BahamutPublishModel message)
        {
            service.PublishBahamutUserNotifyMessage("Vege", message);
        }
    }
}
