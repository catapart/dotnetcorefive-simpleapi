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
        private int TotalBytes;


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
            Schemas.SocketError error = new Schemas.SocketError(){ ErrorCode = "WP_001", Message = "Progress Sessions do not expect messages from the client." };
            SendMessage(socket, error);
        }

        // Custom functionality
        public async Task UpdateProgress(string sessionKey, int value, int total = -1, string stepId = "")
        {
            Schemas.ProgressResponse response = new Schemas.ProgressResponse()
            {
                SessionKey = sessionKey,
                UnitsCompleted = value,
                UnitTotal = total,
                StepID = stepId
            };

            await SendMessageToAll(sessionKey, response);
        }
    }
}
