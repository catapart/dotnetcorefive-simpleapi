using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SimpleAPI_NetCore50.Data;
using SimpleAPI_NetCore50.Websockets;

namespace SimpleAPI_NetCore50.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebsocketController : Controller
    {
        private readonly SimpleApiContext DatabaseContext;
        private readonly SocketSessionService ProgressSessionService;
        private readonly SocketSessionService VideoSessionService;

        public WebsocketController(SimpleApiContext context, ProgressSocketSessionService progressSessionService, VideoSocketSessionService videoSessionService)
        {
            DatabaseContext = context;
            ProgressSessionService = progressSessionService;
            VideoSessionService = videoSessionService;
        }

        [HttpGet("{sessionType}")]
        public async Task<ActionResult> OpenSessionGet(string sessionType)
        {
            string sessionKey = this.PrepareSession(sessionType);
            return CreatedAtAction(nameof(OpenSessionGet), sessionKey);
        }
        [HttpGet("progress/{unitTotal?}")]
        public async Task<ActionResult> OpenProgressSession(int unitTotal = -1)
        {
            string sessionKey = this.PrepareSession("progress", unitTotal);
            return CreatedAtAction(nameof(OpenSessionGet), sessionKey);
        }

        [HttpPost]
        public async Task<ActionResult> OpenSessionPost(Schemas.SocketSessionRequest socketSessionRequest)
        {
            string sessionKey = this.PrepareSession(socketSessionRequest.SessionType, socketSessionRequest.UnitTotal);

            return CreatedAtAction(nameof(OpenSessionPost), sessionKey);
        }

        private string PrepareSession(string sessionType, int unitTotal = -1)
        {
            List<ISessionAttribute> attributes = new List<ISessionAttribute>();

            WebsocketSessionType type = SocketSession.GetSessionType(sessionType);
            if (type == WebsocketSessionType.Unknown)
            {
                throw new Exception("Unknown Session Type: " + sessionType);
            }

            if (type == WebsocketSessionType.Progress)
            {
                if(unitTotal > -1)
                {
                    attributes.Add(new SessionAttribute<int>("unitTotal", unitTotal));
                }
                string sessionKey = ProgressSessionService.PrepareNewSession(attributes.ToArray());
                return sessionKey;
            }
            else if (type == WebsocketSessionType.Message)
            {
                //attributes.Add(new SessionAttribute<List<Schemas.SocketSessionMessageRequest>>("message", new List<Schemas.SocketSessionMessageRequest>()));
                //string sessionKey = ProgressSessionService.PrepareNewSession(attributes.ToArray());
                //return sessionKey;
            }
            else if(type == WebsocketSessionType.Stream)
            {
                attributes.Add(new SessionAttribute<List<Schemas.SocketSessionMessageResponse>>("messages", new List<Schemas.SocketSessionMessageResponse>()));
                string sessionKey = VideoSessionService.PrepareNewSession(attributes.ToArray());
                return sessionKey;
            }

            throw new Exception("Unknown Session Type: " + sessionType);
        }
    }
}
