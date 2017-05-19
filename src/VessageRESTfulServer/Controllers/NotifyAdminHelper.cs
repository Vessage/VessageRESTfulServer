using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BahamutService.Service;
using Newtonsoft.Json;
using VessageRESTfulServer.Models;
using VessageRESTfulServer.Services;
using static UMengTools.UMengMessageModel;

namespace VessageRESTfulServer.Controllers
{
    public class NotifyAdminHelper
    {

        private static IDictionary<string, string> adminUserId = new Dictionary<string, string>();

        static public async Task NotifyAdminNewAccountRegistedAsync(string newAccountId, UserService userService)
        {
            try
            {
                var enableNotify = bool.Parse(Startup.VGConfiguration["VGConfig:UserRegistedNotifyAdmin:enable"]);
                if (enableNotify)
                {
                    var notifyAdmins = await LoadAdminUsersAsync(userService);
                    var msg = string.Format("新用户注册:{0}", newAccountId);
                    PostBahamutNotification(notifyAdmins, msg);
                }
            }
            catch (System.Exception)
            {
            }
        }

        static public async Task NotifyAdminUserInviteNewMobileAccount(VessageUser sender, string newMobile, UserService userService)
        {
            try
            {
                var enableNotify = bool.Parse(Startup.VGConfiguration["VGConfig:UserRegistedNotifyAdmin:enable"]);
                if (enableNotify)
                {
                    var nick = (sender == null || string.IsNullOrWhiteSpace(sender.Nick)) ? "Unknow Nick" : sender.Nick;
                    var mobile = (sender == null || string.IsNullOrWhiteSpace(sender.Mobile)) ? "NoMobile" : sender.Mobile;
                    var notifyAdmins = await LoadAdminUsersAsync(userService);
                    var msg = string.Format("用户{0},{1}邀请手机好友:{2}", nick, mobile, newMobile);
                    PostBahamutNotification(notifyAdmins, msg);
                }
            }
            catch (System.Exception)
            {
            }
        }

        static private async Task<IEnumerable<string>> LoadAdminUsersAsync(UserService userService)
        {
            var admins = Startup.VGConfiguration.GetSection("VGConfig:UserRegistedNotifyAdmin:Admins").GetChildren();
            var notifyAdmins = new List<string>();
            foreach (var admin in admins)
            {
                var adminAccountId = admin.Value;
                var userId = "";
                try
                {
                    userId = adminUserId[adminAccountId];
                }
                catch (System.Exception)
                {
                    var adminUser = await userService.GetUserOfAccountId(adminAccountId);
                    if (adminUser != null)
                    {
                        userId = adminUser.Id.ToString();
                        adminUserId.Add(adminAccountId, userId);
                    }
                }
                if (string.IsNullOrEmpty(userId) == false)
                {
                    notifyAdmins.Add(userId);
                }
            }
            return notifyAdmins;
        }

        static public void PostBahamutNotification(IEnumerable<string> notifyAdmins, string msg)
        {
            if (notifyAdmins.Count() == 0)
            {
                return;
            }

            var umodel = new UMengTools.UMengMessageModel
            {
                apsPayload = new APSPayload
                {
                    aps = new APS
                    {
                        alert = new { loc_key = msg }
                    },
                    custom = "ActivityUpdatedNotify"
                },
                androidPayload = new AndroidPayload
                {
                    body = new ABody
                    {
                        builder_id = 2,
                        after_open = "go_custom",
                        custom = "ActivityUpdatedNotify",
                        text = notifyAdmins.First()
                    },
                    extra = new { acName = "通知管理员", acMsg = msg }
                }
            };

            var notifyMsg = new BahamutPublishModel
            {
                NotifyInfo = umodel.toMiniJson(),
                NotifyType = "ActivityUpdatedNotify",
                ToUser = notifyAdmins.Count() > 1 ? string.Join(",", notifyAdmins) : notifyAdmins.First()
            };
            Startup.ServicesProvider.GetBahamutPubSubService().PublishVegeNotifyMessage(notifyMsg);
        }
    }
}