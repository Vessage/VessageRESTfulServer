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
                var data = await AppServiceProvider.GetActivityService().GetActivityBoardData(UserObjectId);
                var result = ActivityBadgeDataToJsonObject(data);
                return result;
            }
            catch (Exception)
            {
                return new object[0];
            }
        }

        private IEnumerable<object> ActivityBadgeDataToJsonObject(ActivityBadgeData d)
        {
            if(d == null || d.Activities == null)
            {
                return new object[0];
            }
            var result = from item in d.Activities select new {
                id = item.AcId,
                badge = item.Badge,
                miniBadge = item.MiniBadge
            };
            return result;
        }
    }
}
