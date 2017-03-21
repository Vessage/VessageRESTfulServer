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
    public class SubscriptionController : APIControllerBase
    {
        [HttpGet()]
        public IEnumerable<object> Get()
        {
            try
            {
                var data = AppServiceProvider.GetSubscriptionService().GetSubscriptionAccounts();
                var result = from d in data select SubscriptionAccountToJsonObject(d);
                return result;
            }
            catch (Exception)
            {
                return new object[0];
            }
        }

        private object SubscriptionAccountToJsonObject(SubAccount d)
        {
            return new
            {
                id = d.UserId,
                title = d.Title,
                desc = d.Desc,
                avatar = d.Avatar
            };
        }
    }
}
