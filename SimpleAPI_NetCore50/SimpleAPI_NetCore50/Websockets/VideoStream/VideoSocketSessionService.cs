using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Text;
using System.Linq;
using SimpleAPI_NetCore50.Schemas;

namespace SimpleAPI_NetCore50.Websockets
{
    public class VideoSocketSessionService : SocketSessionService
    {
        // Overrides
        public VideoSocketSessionService(IConfiguration configuration, Services.FileService fileService) : base(configuration, fileService)
        {
        }

        public async override Task<SessionSocket> JoinSession(HttpContext context, string sessionType, string sessionKey)
        {

            SocketSession targetSession = GetSessionByKey(sessionKey);
            if (targetSession == null)
            {
                targetSession = this.CreateSession(sessionType, sessionKey);
            }

            WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();

            SessionSocket sessionSocket = targetSession.AddWebSocket(socket);

            string hostId = sessionSocket.Token.SocketId;
            if (targetSession.GetSockets().Count == 1)
            {
                targetSession.SetAttribute("hostId", hostId);
            }
            else
            {
                // alert host that someone has connected
                hostId = targetSession.GetAttributeValue<string>("hostId");

                SocketSessionMessageRequest messageRequest = new SocketSessionMessageRequest()
                {
                    Type = SocketSessionMessageType.StatusUpdate,
                    Message = System.Text.Json.JsonSerializer.Serialize(new { status = "connect", peer = sessionSocket.Token })
                };
                SendMessage(sessionKey, hostId, messageRequest);
            }

            // alert requester session token and current participants
            Models.SocketToken[] participants = targetSession.GetSockets().Select(pair => pair.Value.Token).ToArray();
            SocketSessionMessageRequest participantMessageRequest = new SocketSessionMessageRequest()
            {
                Type = SocketSessionMessageType.Property,
                Message = System.Text.Json.JsonSerializer.Serialize(new { HostId = hostId, SessionToken = sessionSocket.Token, Participants = participants })
            };
            SendMessage(sessionSocket, participantMessageRequest);

            return sessionSocket;
        }

        public async override Task ReceiveMessage(string sessionKey, SessionSocket sessionSocket, WebSocketReceiveResult result, byte[] buffer)
        {
            try
            {
                SocketSession socketSession = GetSessionByKey(sessionKey);
                List<SocketSessionMessageResponse> messages = socketSession.GetAttributeValue<List<SocketSessionMessageResponse>>("messages");

                string serializedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                SocketSessionMessageRequest messageRequest = System.Text.Json.JsonSerializer.Deserialize<SocketSessionMessageRequest>(serializedMessage);
                if(messageRequest.Type == null)
                {
                    messageRequest.Type = SocketSessionMessageType.Text;
                }

                string socketId = sessionSocket.Token.SocketId;
                string displayName = sessionSocket.Token.DisplayName;

                string message = "";
                switch(messageRequest.Type)
                {
                    case SocketSessionMessageType.StatusUpdate:
                        message = messageRequest.Message;
                        break;
                    case SocketSessionMessageType.Text:
                        message = $"{displayName} ({socketId}) said: {messageRequest.Message}";
                        break;
                    case SocketSessionMessageType.Reaction:
                        if (messageRequest.TargetMessageId == null)
                        {
                            throw new Exception("Reaction must provide TargetMessageId value.");
                        }
                        break;
                    default:
                        throw new Exception("Unknown Message Type: " + messageRequest.Type);
                }

                SocketSessionMessageResponse messageResponse = new Schemas.SocketSessionMessageResponse()
                {
                    MessageId = messages.Count,
                    Message = message,
                    SenderId = sessionSocket.Token.SocketId,
                    Recipients = messageRequest.Recipients
                };

                messages.Add(messageResponse);

                if (messageRequest.Recipients.Length == 0)
                {
                    SendMessageToPeers(sessionKey, socketId, message);
                }
                else
                {
                    SendMessage(sessionKey, messageRequest.Recipients, message);
                }
            }
            catch(Exception exception)
            {
                Schemas.SocketError error = new Schemas.SocketError() { ErrorCode = "WP_002", Message = "An error occurred while processing the message" };
                SendMessage(sessionSocket.Socket, error);
            }


        }
    }
}
