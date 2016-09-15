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
        public static string AppChannelId { get { return Configuration[string.Format("AppChannel:{0}:channel", Appkey)]; } }
        public static string Appname { get { return Configuration["Data:App:appname"]; } }
        public static string AppRegion { get { return Configuration["Data:App:region"]; } }

        public static string ConfigRoot { get { return Configuration["Data:ConfigRoot"]; } }

        public static string RegistNewUserApiUrl { get { return Configuration["Data:RegistNewUserApiUrl"]; } }
        public static string ServiceApiUrl { get { return Configuration["Data:ServiceApiUrl"]; } }
        public static string ServiceApiUrlRoute { get { return ServiceApiUrl + "/api"; } }

        public static string AuthServerUrl { get { return Configuration["Data:AuthServer:url"]; } }
        public static string FileApiUrl { get { return Configuration["Data:FileServer:url"]; } }
        public static string ChicagoServerAddress { get { return Configuration["Data:ChicagoServer:host"]; } }
        public static int ChicagoServerPort { get { return int.Parse(Configuration["Data:ChicagoServer:port"]); } }

        public static IDictionary<string, string> ValidatedUsers { get; private set; }
        
        public static bool IsProduction { get { return ServerHostingEnvironment.IsProduction(); } }

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
            var appChannelConfig = baseConfig["Data:AppChannelConfig"];
            builder.AddJsonFile(configFile, true, true);
            builder.AddJsonFile(logConfig, true, true);
            builder.AddJsonFile(appChannelConfig, true, true);
            ServerHostingEnvironment = env;
            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
            
        }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            var TokenServerClientManager = DBClientManagerBuilder.GenerateRedisConnectionMultiplexer(Configuration.GetSection("Data:TokenServer"));
            var redis = DBClientManagerBuilder.GenerateRedisConnectionMultiplexer(Configuration.GetSection("Data:ControlServiceServer"));
            BahamutAppInsanceMonitorManager.Instance.InitManager(redis);
            services.AddSingleton(new ServerControlManagementService(redis));
            services.AddSingleton(new TokenService(TokenServerClientManager));

            services.AddMvc(config => {
                config.Filters.Add(new BahamutAspNetCommon.LogExceptionFilter());
            }).AddJsonOptions(op =>
            {
                op.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                op.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                op.SerializerSettings.Formatting = Formatting.None;
            });

            //business services
            var mongoClient = DBClientManagerBuilder.GeneratePoolMongodbClient(Configuration.GetSection("Data:VessageDBServer"));
            services.AddSingleton(new UserService(mongoClient));
            services.AddSingleton(new VessageService(mongoClient));
            services.AddSingleton(new SharedService(mongoClient));
            services.AddSingleton(new ActivityService(mongoClient));
            services.AddSingleton(new GroupChatService(mongoClient));

            //pubsub manager
            var pbClientManager = DBClientManagerBuilder.GenerateRedisConnectionMultiplexer(Configuration.GetSection("Data:MessagePubSubServer"));

            var pbService = new BahamutPubSubService(pbClientManager);
            services.AddSingleton(pbService);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            
            ServicesProvider = app.ApplicationServices;

            //Log
            var logConfig = new LoggingConfiguration();
            LoggerLoaderHelper.LoadLoggerToLoggingConfig(logConfig, Configuration, "Logger:fileLoggers");
            if (env.IsDevelopment())
            {
                LoggerLoaderHelper.AddConsoleLoggerToLogginConfig(logConfig);
            }
            LogManager.Configuration = logConfig;

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

            //Regist App Instance
            BahamutAppInstanceRegister.RegistAppInstance(ServicesProvider.GetServerControlManagementService(), new BahamutAppInstance()
            {
                Appkey = Appkey,
                InstanceServiceUrl = ServiceApiUrl,
                Region = AppRegion,
                Channel = AppChannelId,
                InfoForClient = JsonConvert.SerializeObject(new
                {
                    apiUrl = ServiceApiUrl
                }, Formatting.None)
            });

            //Startup
            LogManager.GetLogger("Main").Info("VG Api Server Started!");
        }
    }

    public static class IPubSubServiceExtension
    {
        public static void PublishVegeNotifyMessage(this BahamutPubSubService service,BahamutPublishModel message)
        {
            service.PublishBahamutUserNotifyMessage(Startup.AppChannelId, message);
        }
    }
}
