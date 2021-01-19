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
    public class WebsocketSessionMiddleware
    {
        protected readonly RequestDelegate _next;


        protected readonly ILogger<WebsocketSessionMiddleware> Logger;
        protected WebsocketSessionService SessionService { get; set; }

        public WebsocketSessionMiddleware(RequestDelegate next, ILogger<WebsocketSessionMiddleware> logger, WebsocketSessionService socketSessionService)
        {
            _next = next;
            Logger = logger;
            SessionService = socketSessionService;
        }

        //public virtual async Task Invoke(HttpContext context)
        //{
        //    if (!context.WebSockets.IsWebSocketRequest)
        //        return;

        //    if (!context.Request.Path.HasValue)
        //    {
        //        return;
        //    }
        //    string path = context.Request.Path;

        //    string[] pathArray = path.Split("/");
        //    string sessionKey = pathArray[1];
        //    if (string.IsNullOrEmpty(sessionKey))
        //    {
        //        return;
        //    }

        //    SessionSocket sessionSocket = await SessionService.JoinSession(context, "unknown", sessionKey);

        //    await Receive(sessionSocket.Socket, async (result, buffer) =>
        //    {
        //        if (result.MessageType == WebSocketMessageType.Text)
        //        {
        //            await SessionService.ReceiveMessage(sessionKey, sessionSocket, result, buffer);
        //            return;
        //        }
        //        else if (result.MessageType == WebSocketMessageType.Close)
        //        {
        //            await SessionService.EndSession(sessionKey);
        //            return;
        //        }

        //    });

        //    await _next.Invoke(context);
        //}


        public virtual async Task Invoke(HttpContext context)
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
                    await ((WebsocketSessionService)SessionService).ReceiveMessage(sessionKey, sessionSocket, result, buffer);
                    return;
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {

                    WebsocketSession targetSession = SessionService.GetSessionByKey(sessionKey);
                    string hostId = targetSession.GetAttributeValue<string>("hostId");

                    // if the host quit, kill the session
                    if (sessionSocket.Token.SocketId == hostId)
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

        protected virtual async Task Receive(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
        {
            var buffer = new byte[1024 * 20];

            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer: new ArraySegment<byte>(buffer), cancellationToken: CancellationToken.None);

                handleMessage(result, buffer);
            }
        }
    }
}
