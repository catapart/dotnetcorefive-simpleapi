using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SimpleAPI_NetCore50.Data;
using SimpleAPI_NetCore50.Websockets;

namespace SimpleAPI_NetCore50.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebsocketController : ControllerBase
    {
        private readonly SimpleApiContext DatabaseContext;
        private readonly SocketSessionService SessionService;

        public WebsocketController(SimpleApiContext context, SocketSessionService sessionService)
        {
            DatabaseContext = context;
            SessionService = sessionService;
        }

        // GET: api/Application
        [HttpGet("{sessionType}")]
        public async Task<ActionResult> OpenSession(string sessionType)
        {
            WebsocketSessionType type = SocketSession.GetSessionType(sessionType);
            if(type == WebsocketSessionType.Unknown)
            {
                throw new Exception("Unknown Session Type: " + sessionType);
            }

            return await Task.FromResult(Ok());
        }


    }
}
