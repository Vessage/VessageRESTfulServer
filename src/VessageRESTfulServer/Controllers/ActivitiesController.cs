using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VessageRESTfulServer.Services;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace VessageRESTfulServer.Controllers
{
    [Route("api/[controller]")]
    public class ActivitiesController : APIControllerBase
    {
        [HttpGet("BoardData")]
        public async Task<IEnumerable<object>> Get()
        {
            try
            {
                var data = await AppServiceProvider.GetActivityService().GetActivityBoardData(UserSessionData.UserId);
                var result = ActivityBadgeDataToJsonObject(data);
                return result;
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.Message);
#endif
                return new object[0];
            }
            

        }

        private class ActivityData
        {
            public string id { get; set; }
            public int badge { get; set; }
            public bool miniBadge { get; set; }
        }

        private IEnumerable<object> ActivityBadgeDataToJsonObject(ActivityBadgeData d)
        {
            var dict = new Dictionary<string, ActivityData>();
            foreach (var badge in d.BadgeValueActivity)
            {
                var strs = badge.Split(':');
                var acId = strs[0];
                var cnt = 0;
                try
                {
                    cnt = int.Parse(strs[1]);
                    if (dict.ContainsKey(acId))
                    {
                        var ad = dict[acId];
                        ad.badge += cnt;
                    }
                    else
                    {
                        dict[acId] = new ActivityData
                        {
                            id = acId,
                            badge = cnt,
                            miniBadge = false
                        };
                    }
                }
                catch (Exception)
                {
                }
            }

            foreach (var miniAc in d.MiniBadgeActivity)
            {
                if(dict.ContainsKey(miniAc))
                {
                    dict[miniAc].miniBadge = true;
                }
                else
                {
                    dict[miniAc] = new ActivityData
                    {
                        id = miniAc,
                        badge = 0,
                        miniBadge = true
                    };
                }
            }

            return dict.Values.ToList();
        }
    }
}
