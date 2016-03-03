using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;

namespace VessageRESTfulServer.Controllers
{
    [Route("api/[controller]")]
    public class VessagesController : APIControllerBase
    {

        [HttpGet("New")]
        public IEnumerable<object> GetNewVessages()
        {
            return new string[] { "value1", "value2" };
        }

        [HttpPut("Got")]
        public void GotNewVessages()
        {

        }

        [HttpGet("Conversation/{cid}")]
        public IEnumerable<object> GetConversationVessages(string cid)
        {
            return new string[] { "value1", "value2" };
        }
        
        [HttpPost]
        public void SendNewVessage(string conversationId, string fileId)
        {
        }
        
        [HttpPut("Read/{vid}")]
        public void ReadVessage(string vid)
        {

        }
        
        [HttpDelete("{id}")]
        public void Delete(string id)
        {
        }
    }
}
