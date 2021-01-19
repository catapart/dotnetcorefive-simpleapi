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
    public class VideoSocketSessionService : WebsocketSessionService
    {
        // Overrides
        public VideoSocketSessionService(IConfiguration configuration, Services.FileService fileService) : base(configuration, fileService)
        {
        }

        public async override Task<SessionSocket> JoinSession(HttpContext context, string sessionType, string sessionKey)
        {

            WebsocketSession targetSession = GetSessionByKey(sessionKey);
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
                hostId = targetSession.GetAttributeValue<string>("hostId");
            }

            // alert requester of their own token, the hosts token and other existing peers
            Models.SocketToken hostToken = targetSession.GetSocketById(hostId).Token;
            Models.SocketToken[] participants = targetSession.GetSockets().Where(pair => pair.Value.Token.SocketId != sessionSocket.Token.SocketId ).Select(pair => pair.Value.Token).ToArray();

            SocketSessionMessageResponse response = new SocketSessionMessageResponse()
            {
                MessageType = SocketSessionMessageType.Greeting,
                Message = System.Text.Json.JsonSerializer.Serialize(new { SessionKey = sessionKey, HostToken = hostToken, Token = sessionSocket.Token, Peers = participants })
            };
            SendMessage(sessionSocket, response);

            return sessionSocket;
        }

        public async override Task ReceiveMessage(string sessionKey, SessionSocket sessionSocket, WebSocketReceiveResult result, byte[] buffer)
        {
            try
            {
                WebsocketSession socketSession = GetSessionByKey(sessionKey);
                List<SocketSessionMessageResponse> messages = socketSession.GetAttributeValue<List<SocketSessionMessageResponse>>("messages");

                string serializedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                SocketSessionMessageRequest messageRequest = System.Text.Json.JsonSerializer.Deserialize<SocketSessionMessageRequest>(serializedMessage);
                if(messageRequest.Type == SocketSessionMessageType.Unknown)
                {
                    messageRequest.Type = SocketSessionMessageType.Text;
                }

                string socketId = sessionSocket.Token.SocketId;
                string displayName = sessionSocket.Token.DisplayName;

                string message = "";
                switch (messageRequest.Type)
                {
                    case SocketSessionMessageType.Introduction:
                        string hostId = socketSession.GetAttributeValue<string>("hostId");
                        Models.SocketToken token = System.Text.Json.JsonSerializer.Deserialize<Models.SocketToken>(messageRequest.Message);
                        sessionSocket.Token.DisplayName = token.DisplayName;
                        sessionSocket.Token.IconUrl = token.IconUrl;

                        if(token.SocketId == hostId)
                        {
                            // if host, no need to request access; just grant access;
                            Schemas.SocketSessionUpdate[] updates = new Schemas.SocketSessionUpdate[]
                            {
                                new Schemas.SocketSessionUpdate()
                                {
                                    Status = "accessgranted",
                                    Peers = new Models.SocketToken[1] { sessionSocket.Token }
                                }
                            };

                            SocketSessionMessageResponse hostAlertResponse = new SocketSessionMessageResponse()
                            {
                                MessageType = SocketSessionMessageType.StatusUpdates,
                                Message = System.Text.Json.JsonSerializer.Serialize(updates)
                            };
                            SendMessage(sessionKey, hostId, hostAlertResponse);
                            return;
                        }
                        message = messageRequest.Message;
                        break;
                    case SocketSessionMessageType.StatusUpdates:
                    case SocketSessionMessageType.Text:
                        message = messageRequest.Message;
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
                    MessageType = messageRequest.Type,
                    Message = message,
                    SenderId = sessionSocket.Token.SocketId,
                    Recipients = messageRequest.Recipients
                };

                messages.Add(messageResponse);

                if (messageRequest.Recipients == null || messageRequest.Recipients.Length == 0)
                {
                    SendMessageToPeers(sessionKey, socketId, messageResponse);
                }
                else
                {
                    SendMessage(sessionKey, messageRequest.Recipients, messageResponse);
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
