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
using SimpleAPI_NetCore50.Models;
using SimpleAPI_NetCore50.Utilities;

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

        public virtual async Task<WebsocketSessionPeer> JoinSession(HttpContext context, string sessionType, string sessionKey)
        {

            WebsocketSession targetSession = GetSessionByKey(sessionKey);
            if (targetSession == null)
            {
                targetSession = this.CreateSession(sessionType, sessionKey);
            }

            WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();

            WebsocketSessionPeer sessionSocket = targetSession.AddPeer(socket);

            string hostId = sessionSocket.Token.PeerId;
            if (targetSession.GetPeers().Count == 1)
            {
                targetSession.SetAttribute("hostId", hostId);
            }
            else
            {
                hostId = targetSession.GetAttributeValue<string>("hostId");
            }

            // alert requester of their own token, the hosts token and other existing peers
            WebsocketSessionPeerToken hostToken = targetSession.GetPeerById(hostId).Token;
            WebsocketSessionPeerToken[] participants = targetSession.GetPeers().Where(pair => pair.Value.Token.PeerId != sessionSocket.Token.PeerId).Select(pair => pair.Value.Token).ToArray();

            WebsocketSessionGreeting greeting = new WebsocketSessionGreeting { SessionKey = sessionKey, HostToken = hostToken, Token = sessionSocket.Token, Peers = participants };
            WebsocketSessionMessageResponse greetingResponse = CreateWebsocketResponseMessage(WebsocketSessionMessageType.Greeting, greeting);
            SendMessage(sessionSocket, greetingResponse);

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
        public async Task SendMessage(WebsocketSessionPeer sessionSocket, object value)
        {
            string message = JsonSerializer.Serialize(value);
            await SendMessage(sessionSocket.Socket, message);
        }
        public async Task SendMessage(WebsocketSessionPeer sessionSocket, string message)
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
            WebsocketSession session = this.GetSessionByKey(sessionKey);
            if (session == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            WebsocketSessionPeer sessionSocket = session.GetPeerById(socketId);
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

            WebsocketSession session = this.GetSessionByKey(sessionKey);
            if (session == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            foreach (var pair in session.GetPeers())
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
            WebsocketSession session = this.GetSessionByKey(sessionKey);
            if (session == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            foreach (var pair in session.GetPeers())
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
                WebsocketSession session = this.GetSessionByKey(sessionKey);
                if (session == null)
                {
                    throw new Exception("Unknown Session requested: " + sessionKey);
                }

                foreach (var pair in session.GetPeers())
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
                WebsocketSession session = this.GetSessionByKey(sessionKey);
                if (session == null)
                {
                    throw new Exception("Unknown Session requested: " + sessionKey);
                }

                foreach (var pair in session.GetPeers())
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
            WebsocketSession session = this.GetSessionByKey(sessionKey);
            if (session == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            foreach (var pair in session.GetPeers())
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
            WebsocketSession session = this.GetSessionByKey(sessionKey);
            if (session == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            foreach (var pair in session.GetPeers())
            {
                if (pair.Value.Socket.State == WebSocketState.Open)
                {
                    await SendMessage(pair.Value.Socket, message);
                }
            }
        }
        public virtual async Task SendMessageToPeers(string sessionKey, WebSocket socket, string message)
        {
            WebsocketSession session = this.GetSessionByKey(sessionKey);
            if (session == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            foreach (var pair in session.GetPeers())
            {
                if (pair.Value.Socket != socket && pair.Value.Socket.State == WebSocketState.Open)
                {
                    await SendMessage(pair.Value.Socket, message);
                }
            }
        }

        // access the raw bytes that were sent through the websocket;
        public virtual async Task ReceiveMessage(string sessionKey, WebsocketSessionPeer sessionPeer, WebSocketReceiveResult result, byte[] buffer)
        {
            string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            await ReceiveMessageString(sessionKey, sessionPeer, result, message);
        }

        // access the websocket data as a string; (if you JSON serialized, you might want to only override this function
        // because you'll know that all of your data will be a string that you are going to deserialize;
        // If you wanted to do something like streaming video via direct byte arrays, you'd want to override the other
        // "receive" function.
        public virtual async Task ReceiveMessageString(string sessionKey, WebsocketSessionPeer peer, WebSocketReceiveResult result, string socketMessage)
        {
            try
            {
                // parse the request to access the data;
                WebsocketSessionMessageRequest messageRequest = System.Text.Json.JsonSerializer.Deserialize<WebsocketSessionMessageRequest>(socketMessage);
                if (messageRequest.Type == WebsocketSessionMessageType.Unknown)
                {
                    messageRequest.Type = WebsocketSessionMessageType.Text;
                }

                // process the request and send the response message, if applicable;
                ProcessMessageRequest(messageRequest, sessionKey, peer);
            }
            catch (Exception exception)
            {
                WebsocketSessionError error = new WebsocketSessionError() { ErrorCode = "WP_002", Message = "An error occurred while processing the message" };
                SendMessage(peer.Socket, error);
            }
        }

        public static WebsocketSessionMessageResponse CreateWebsocketResponseMessage(WebsocketSessionMessageType type, object data)
        {
            return new WebsocketSessionMessageResponse()
            {
                MessageType = type,
                Message = System.Text.Json.JsonSerializer.Serialize(data)
            };
        }

        public static WebsocketSessionUpdate[] CreatePeerUpdates(WebsocketSessionUpdateStatus status, WebsocketSessionPeerToken peerToken)
        {
            WebsocketSessionPeerToken[] peerTokens = new WebsocketSessionPeerToken[]
            {
                peerToken
            };

            return CreatePeerUpdates(status, peerTokens);
        }
        public static WebsocketSessionUpdate[] CreatePeerUpdates(WebsocketSessionUpdateStatus status, WebsocketSessionPeerToken[] peerTokens)
        {
            WebsocketSessionUpdate[] updates = new WebsocketSessionUpdate[]{
                new WebsocketSessionUpdate
                {
                    Status = GetSessionUpdateStatusString(status),
                    Peers = peerTokens
                }
            };
            return updates;
        }

        public static string GetSessionUpdateStatusString(WebsocketSessionUpdateStatus status)
        {
            switch(status)
            {
                case WebsocketSessionUpdateStatus.Connect:
                    return "connect";
                case WebsocketSessionUpdateStatus.Disconnect:
                    return "disconnect";
                case WebsocketSessionUpdateStatus.AccessGranted:
                    return "accessgranted";
                case WebsocketSessionUpdateStatus.AccessDenied:
                    return "accessdenied";
                case WebsocketSessionUpdateStatus.Progress:
                    return "progress";
                case WebsocketSessionUpdateStatus.Unknown:
                default:
                    return "unknown";
            }
        }

        private Task ProcessMessageRequest(WebsocketSessionMessageRequest request, string sessionKey, WebsocketSessionPeer peer)
        {
            // get a reference to the session
            WebsocketSession session = GetSessionByKey(sessionKey);

            // handle the different types of messages;
            string message = "";
            switch (request.Type)
            {
                case WebsocketSessionMessageType.Introduction:
                    string hostId = session.GetAttributeValue<string>("hostId");
                    WebsocketSessionPeerToken token = System.Text.Json.JsonSerializer.Deserialize<Models.WebsocketSessionPeerToken>(request.Message);
                    peer.Token.DisplayName = token.DisplayName;
                    peer.Token.IconUrl = token.IconUrl;

                    if (token.PeerId == hostId)
                    {
                        // if host, no need to request access; just grant access;
                        WebsocketSessionUpdate[] updates = CreatePeerUpdates(WebsocketSessionUpdateStatus.AccessGranted, peer.Token);
                        WebsocketSessionMessageResponse hostAlertResponse = CreateWebsocketResponseMessage(WebsocketSessionMessageType.StatusUpdates, updates);
                        SendMessage(sessionKey, hostId, hostAlertResponse);
                        return Task.CompletedTask;
                    }
                    message = request.Message;
                    break;
                case WebsocketSessionMessageType.StatusUpdates:
                case WebsocketSessionMessageType.Text:
                    message = request.Message;
                    break;
                case WebsocketSessionMessageType.Reaction:
                    if (request.TargetMessageId == null)
                    {
                        throw new Exception("Reaction must provide TargetMessageId value.");
                    }
                    break;
                default:
                    throw new Exception("Unknown Message Type: " + request.Type);
            }

            List<WebsocketSessionMessageResponse> messages = session.GetAttributeValue<List<WebsocketSessionMessageResponse>>("messages");

            WebsocketSessionMessageResponse messageResponse = new WebsocketSessionMessageResponse()
            {
                MessageId = messages.Count,
                MessageType = request.Type,
                Message = message,
                SenderId = peer.Token.PeerId,
                Recipients = request.Recipients
            };
            messages.Add(messageResponse);

            if (request.Recipients == null || request.Recipients.Length == 0)
            {
                SendMessageToPeers(sessionKey, peer.Token.PeerId, messageResponse);
            }
            else
            {
                SendMessage(sessionKey, request.Recipients, messageResponse);
            }
            return Task.CompletedTask;
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
