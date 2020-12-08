using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;

namespace SimpleAPI_NetCore50.Websockets
{
    public class ProgressSocketSessionService : SocketSessionService
    {
        public async override Task<SessionSocket> JoinSession(HttpContext context, string sessionType, string sessionKey)
        {

            SocketSession targetSession = GetSessionByKey(sessionKey);
            if (targetSession == null)
            {
                targetSession = this.CreateSession(sessionType, sessionKey);
            }

            if(targetSession.GetSockets().Count > 0)
            {
                throw new Exception("Unable to join Progress session that already has a subscriber.");
            }

            WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();

            SessionSocket sessionSocket = targetSession.AddWebSocket(socket);

            return sessionSocket;
        }

        public async override Task ReceiveMessage(WebSocket socket, WebSocketReceiveResult result, byte[] buffer)
        {

        }

        // Custom functionality
        public async Task UpdateProgress(float value, float total)
        {
            //ProgressResponse progressResponse = new ProgressResponse();
            //progressResponse.ProgressValue = value;
            //progressResponse.ProgressTotal = total;

            //string message = JsonSerializer.Serialize(progressResponse);

            //await SendMessageToAllAsync(message);
        }
    }
}
