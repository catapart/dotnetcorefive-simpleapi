using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SimpleAPI_NetCore50.Schemas;

namespace SimpleAPI_NetCore50.Websockets
{
    public class WebsocketSessionService
    {
        protected readonly IConfiguration AppConfig;
        private ConcurrentDictionary<string, WebsocketSession> Sessions = new ConcurrentDictionary<string, WebsocketSession>();
        private ConcurrentDictionary<string, IProcessArtifact[]> PreparedSessions = new ConcurrentDictionary<string, IProcessArtifact[]>();
        protected readonly Services.FileService FileService;

        public WebsocketSessionService(IConfiguration configuration, Services.FileService fileService)
        {
            AppConfig = configuration;
            FileService = fileService;
        }

        public WebsocketSession GetSessionByKey(string key)
        {
            return Sessions.FirstOrDefault(pair => pair.Key == key).Value;
        }
        public ConcurrentDictionary<string, WebsocketSession> GetAll()
        {
            return Sessions;
        }

        public virtual string GetKey(WebsocketSession session)
        {
            return Sessions.FirstOrDefault(pair => pair.Value == session).Key;
        }
        public KeyValuePair<string, IProcessArtifact[]> GetPreparedSession(string key)
        {
            return PreparedSessions.FirstOrDefault(pair => pair.Key == key);
        }

        public virtual string PrepareNewSession(params IProcessArtifact[] attributes)
        {
            string sessionKey = CreateSessionKey();
            PreparedSessions.TryAdd(sessionKey, attributes);
            return sessionKey;
        }

        //public virtual async Task<SessionSocket> JoinSession(HttpContext context, string sessionType, string sessionKey)
        //{
        //    WebsocketSession targetSession = GetSessionByKey(sessionKey);
        //    if (targetSession == null)
        //    {
        //        targetSession = this.CreateSession(sessionType, sessionKey);
        //    }

        //    WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();

        //    SessionSocket sessionSocket = targetSession.AddWebSocket(socket);


        //    return sessionSocket;
        //}

        public virtual async Task<SessionSocket> JoinSession(HttpContext context, string sessionType, string sessionKey)
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
            Models.SocketToken[] participants = targetSession.GetSockets().Where(pair => pair.Value.Token.SocketId != sessionSocket.Token.SocketId).Select(pair => pair.Value.Token).ToArray();

            SocketSessionMessageResponse response = new SocketSessionMessageResponse()
            {
                MessageType = SocketSessionMessageType.Greeting,
                Message = System.Text.Json.JsonSerializer.Serialize(new { SessionKey = sessionKey, HostToken = hostToken, Token = sessionSocket.Token, Peers = participants })
            };
            SendMessage(sessionSocket, response);

            return sessionSocket;
        }

        protected virtual WebsocketSession CreateSession(string sessionType, string sessionKey)
        {

            KeyValuePair<string, IProcessArtifact[]> preparedSession = GetPreparedSession(sessionKey);
            if (preparedSession.Equals(default(KeyValuePair<string, IProcessArtifact[]>)))
            {
                throw new Exception("Unknown Session: " + sessionKey);
            }

            WebsocketSession targetSession = new WebsocketSession();
            targetSession.SessionKey = sessionKey;
            targetSession.SessionType = WebsocketSession.GetSessionType(sessionType);
            Sessions.TryAdd(sessionKey, targetSession);
            targetSession.SetAttributes(preparedSession.Value);

            PreparedSessions.TryRemove(preparedSession);

            return targetSession;
        }

        public virtual async Task EndSession(string sessionKey)
        {
            WebsocketSession session = GetSessionByKey(sessionKey);
            await session.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the Session Service.", CancellationToken.None);

            WebsocketSession removedSession;
            Sessions.TryRemove(sessionKey, out removedSession);

            KeyValuePair<string, IProcessArtifact[]> preparedSession = GetPreparedSession(sessionKey);
            if (!preparedSession.Equals(default(KeyValuePair<string, IProcessArtifact[]>)))
            {
                PreparedSessions.TryRemove(preparedSession);
            }
        }

        #region SendMessage Overloads
        public async Task SendMessage(WebSocket socket, object value)
        {
            string message = JsonSerializer.Serialize(value);
            await SendMessage(socket, message);
        }
        public async Task SendMessage(SessionSocket sessionSocket, object value)
        {
            string message = JsonSerializer.Serialize(value);
            await SendMessage(sessionSocket.Socket, message);
        }
        public async Task SendMessage(SessionSocket sessionSocket, string message)
        {
            await SendMessage(sessionSocket.Socket, message);
        }
        public async Task SendMessage(string sessionKey, string socketId, object value)
        {
            string message = JsonSerializer.Serialize(value);
            await SendMessage(sessionKey, socketId, message);
        }
        public async Task SendMessage(string sessionKey, string socketId, string message)
        {
            WebsocketSession socketSession = this.GetSessionByKey(sessionKey);
            if (socketSession == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            SessionSocket sessionSocket = socketSession.GetSocketById(socketId);
            if (sessionSocket == null)
            {
                throw new Exception("Unknown Socket requested: " + socketId);
            }

            await SendMessage(sessionSocket.Socket, message);
        }
        public async Task SendMessage(string sessionKey, IEnumerable<string> socketIds, object value)
        {
            string message = JsonSerializer.Serialize(value);
            await SendMessage(sessionKey, socketIds, message);
        }
        public async Task SendMessage(string sessionKey, IEnumerable<string> socketIds, string message)
        {

            WebsocketSession socketSession = this.GetSessionByKey(sessionKey);
            if (socketSession == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            foreach (var pair in socketSession.GetSockets())
            {
                if (socketIds.Contains(pair.Key) && pair.Value.Socket.State == WebSocketState.Open)
                {
                    await SendMessage(pair.Value.Socket, message);
                }
            }
        }
        public async Task SendMessage(string sessionKey, IEnumerable<WebSocket> sockets, object value)
        {
            string message = JsonSerializer.Serialize(value);
            await SendMessage(sessionKey, sockets, message);
        }
        public async Task SendMessage(string sessionKey, IEnumerable<WebSocket> sockets, string message)
        {
            List<Task> tasks = new List<Task>();
            WebsocketSession socketSession = this.GetSessionByKey(sessionKey);
            if (socketSession == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            foreach (var pair in socketSession.GetSockets())
            {
                if (sockets.Contains(pair.Value.Socket) && pair.Value.Socket.State == WebSocketState.Open)
                {
                    tasks.Add(SendMessage(pair.Value.Socket, message));
                }
            }
            await Task.WhenAll(tasks);
        }
        public async Task SendMessage(IEnumerable<string> sessionKeys, IEnumerable<string> socketIds, object value)
        {
            string message = JsonSerializer.Serialize(value);
            await SendMessage(sessionKeys, socketIds, message);
        }
        public async Task SendMessage(IEnumerable<string> sessionKeys, IEnumerable<string> socketIds, string message)
        {
            List<Task> tasks = new List<Task>();
            foreach (string sessionKey in sessionKeys)
            {
                WebsocketSession socketSession = this.GetSessionByKey(sessionKey);
                if (socketSession == null)
                {
                    throw new Exception("Unknown Session requested: " + sessionKey);
                }

                foreach (var pair in socketSession.GetSockets())
                {
                    if (socketIds.Contains(pair.Key) && pair.Value.Socket.State == WebSocketState.Open)
                    {
                        tasks.Add(SendMessage(pair.Value.Socket, message));
                    }
                }
            }

            await Task.WhenAll(tasks);
        }
        public async Task SendMessage(IEnumerable<string> sessionKeys, IEnumerable<WebSocket> sockets, object value)
        {
            string message = JsonSerializer.Serialize(value);
            await SendMessage(sessionKeys, sockets, message);
        }
        public async Task SendMessage(IEnumerable<string> sessionKeys, IEnumerable<WebSocket> sockets, string message)
        {
            List<Task> tasks = new List<Task>();
            foreach (string sessionKey in sessionKeys)
            {
                WebsocketSession socketSession = this.GetSessionByKey(sessionKey);
                if (socketSession == null)
                {
                    throw new Exception("Unknown Session requested: " + sessionKey);
                }

                foreach (var pair in socketSession.GetSockets())
                {
                    if (sockets.Contains(pair.Value.Socket) && pair.Value.Socket.State == WebSocketState.Open)
                    {
                        tasks.Add(SendMessage(pair.Value.Socket, message));
                    }
                }
            }

            await Task.WhenAll(tasks);
        }

        public virtual async Task SendMessageToAll(string sessionKey, object value)
        {
            string message = JsonSerializer.Serialize(value);
            await SendMessageToAll(sessionKey, message);
        }

        public virtual async Task SendMessageToPeers(string sessionKey, string sendingSocketId, object value)
        {
            string message = JsonSerializer.Serialize(value);
            await SendMessageToPeers(sessionKey, sendingSocketId, message);
        }
        public virtual async Task SendMessageToPeers(string sessionKey, string sendingSocketId, string message)
        {
            WebsocketSession socketSession = this.GetSessionByKey(sessionKey);
            if (socketSession == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            foreach (var pair in socketSession.GetSockets())
            {
                if (pair.Key != sendingSocketId && pair.Value.Socket.State == WebSocketState.Open)
                {
                    await SendMessage(pair.Value.Socket, message);
                }

            }
        }
        public virtual async Task SendMessageToPeers(string sessionKey, WebSocket socket, object value)
        {
            string message = JsonSerializer.Serialize(value);
            await SendMessageToPeers(sessionKey, socket, message);
        }
        #endregion

        public virtual async Task SendMessage(WebSocket socket, string message)
        {
            if (socket.State != WebSocketState.Open)
            {
                return;
            }

            await socket.SendAsync(buffer: new ArraySegment<byte>(array: Encoding.ASCII.GetBytes(message), offset: 0, count: message.Length),
                                   messageType: WebSocketMessageType.Text,
                                   endOfMessage: true,
                                   cancellationToken: CancellationToken.None);
        }
        public virtual async Task SendMessageToAll(string sessionKey, string message)
        {
            WebsocketSession socketSession = this.GetSessionByKey(sessionKey);
            if (socketSession == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            foreach (var pair in socketSession.GetSockets())
            {
                if (pair.Value.Socket.State == WebSocketState.Open)
                {
                    await SendMessage(pair.Value.Socket, message);
                }
            }
        }
        public virtual async Task SendMessageToPeers(string sessionKey, WebSocket socket, string message)
        {
            WebsocketSession socketSession = this.GetSessionByKey(sessionKey);
            if (socketSession == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            foreach (var pair in socketSession.GetSockets())
            {
                if (pair.Value.Socket != socket && pair.Value.Socket.State == WebSocketState.Open)
                {
                    await SendMessage(pair.Value.Socket, message);
                }
            }
        }

        //public virtual async Task ReceiveMessage(string sessionKey, SessionSocket sessionSocket, WebSocketReceiveResult result, byte[] buffer)
        //{
        //    // intentionally blank
        //    // you don't have to do anything with messages you get from the client
        //    // but if you need to, you can override this function. Otherwise it's
        //    // just an empty execution.
        //}


        public virtual async Task ReceiveMessage(string sessionKey, SessionSocket sessionSocket, WebSocketReceiveResult result, byte[] buffer)
        {
            try
            {
                WebsocketSession socketSession = GetSessionByKey(sessionKey);
                List<SocketSessionMessageResponse> messages = socketSession.GetAttributeValue<List<SocketSessionMessageResponse>>("messages");

                string serializedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                SocketSessionMessageRequest messageRequest = System.Text.Json.JsonSerializer.Deserialize<SocketSessionMessageRequest>(serializedMessage);
                if (messageRequest.Type == SocketSessionMessageType.Unknown)
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

                        if (token.SocketId == hostId)
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
            catch (Exception exception)
            {
                Schemas.SocketError error = new Schemas.SocketError() { ErrorCode = "WP_002", Message = "An error occurred while processing the message" };
                SendMessage(sessionSocket.Socket, error);
            }


        }

        private static string CreateSessionKey()
        {
            return GenerateRandomString(5);
        }
        private static string GenerateRandomString(int length, bool useNumbers = true)
        {
            Random random = new Random();
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            if (useNumbers)
            {
                chars += "0123456789";
            }

            string randomString = "";
            for (int i = 0; i < length; i++)
            {
                char character = chars[random.Next(chars.Length)];
                randomString += character;
            }

            return randomString;
        }
    }
}
