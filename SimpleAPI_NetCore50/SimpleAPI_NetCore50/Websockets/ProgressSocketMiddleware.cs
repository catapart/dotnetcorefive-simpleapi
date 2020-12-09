using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;

namespace SimpleAPI_NetCore50.Websockets
{
    public class ProgressSocketMiddleware : SocketMiddleware
    {

        public ProgressSocketMiddleware(RequestDelegate next, ILogger<SocketMiddleware> logger, ProgressSocketSessionService socketSessionService) : base(next, logger, socketSessionService)
        {

        }

        public override async Task Invoke(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
                return;

            if (!context.Request.Path.HasValue)
            {
                return;
            }
            string path = context.Request.Path;

            string[] pathArray = path.Split("/");
            string sessionKey = pathArray[1];
            if (string.IsNullOrEmpty(sessionKey))
            {
                return;
            }

            SessionSocket sessionSocket = await SessionService.JoinSession(context, "progress", sessionKey);

            await Receive(sessionSocket.Socket, async (result, buffer) =>
            {
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    await SessionService.ReceiveMessage(sessionSocket.Socket, result, buffer);
                    return;
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await SessionService.EndSession(sessionKey);
                    return;
                }

            });

            await _next.Invoke(context);
        }
    }
}
