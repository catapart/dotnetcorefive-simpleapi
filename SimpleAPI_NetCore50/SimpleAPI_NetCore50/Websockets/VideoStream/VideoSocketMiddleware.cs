using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using SimpleAPI_NetCore50.Websockets;

namespace SimpleAPI_NetCore50.Websockets
{
    public class VideoSocketMiddleware : WebsocketSessionMiddleware
    {

        public VideoSocketMiddleware(RequestDelegate next, ILogger<WebsocketSessionMiddleware> logger, VideoSocketSessionService socketSessionService) : base(next, logger, socketSessionService)
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

            SessionSocket sessionSocket = await SessionService.JoinSession(context, "stream", sessionKey);

            await Receive(sessionSocket.Socket, async (result, buffer) =>
            {
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    await ((VideoSocketSessionService)SessionService).ReceiveMessage(sessionKey, sessionSocket, result, buffer);
                    return;
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {

                    WebsocketSession targetSession = SessionService.GetSessionByKey(sessionKey);
                    string hostId = targetSession.GetAttributeValue<string>("hostId");

                    // if the host quit, kill the session
                    if(sessionSocket.Token.SocketId == hostId)
                    {
                        await SessionService.EndSession(sessionKey);
                        return;
                    }

                    // otherwise, disconnect and alert host that someone has disconnected
                    await targetSession.RemoveSessionSocket(sessionSocket.Token.SocketId);
                    Schemas.SocketSessionMessageResponse response = new Schemas.SocketSessionMessageResponse()
                    {
                        MessageType = Schemas.SocketSessionMessageType.StatusUpdates,
                        Message = System.Text.Json.JsonSerializer.Serialize(new object[] { new { Status = "disconnect", Peers = new Models.SocketToken[1] { sessionSocket.Token } } })
                    };
                    SessionService.SendMessageToPeers(sessionKey, sessionSocket.Token.SocketId, response);
                }

            });

            await _next.Invoke(context);
        }
    }
}
