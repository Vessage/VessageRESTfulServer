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
                var result = from d in data select ActivityBadgeDataToJsonObject(d);
                return result;
            }
            catch (Exception)
            {
                return new object[0];
            }
        }

        private object ActivityBadgeDataToJsonObject(ActivityBadgeData d)
        {
            return new
            {
                id = d.AcId,
                badge = d.Badge,
                miniBadge = d.MiniBadge,
                msg = d.Message
            };
        }
    }
}
