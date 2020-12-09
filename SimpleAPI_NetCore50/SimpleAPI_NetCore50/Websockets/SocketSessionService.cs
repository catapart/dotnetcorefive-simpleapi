using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace SimpleAPI_NetCore50.Websockets
{
    public class SocketSessionService
    {
        private ConcurrentDictionary<string, SocketSession> Sessions = new ConcurrentDictionary<string, SocketSession>();
        private ConcurrentDictionary<string, ISessionAttribute[]> PreparedSessions = new ConcurrentDictionary<string, ISessionAttribute[]>();

        public SocketSession GetSessionByKey(string key)
        {
            return Sessions.FirstOrDefault(pair => pair.Key == key).Value;
        }
        public ConcurrentDictionary<string, SocketSession> GetAll()
        {
            return Sessions;
        }

        public virtual string GetKey(SocketSession session)
        {
            return Sessions.FirstOrDefault(pair => pair.Value == session).Key;
        }
        public KeyValuePair<string, ISessionAttribute[]> GetPreparedSession(string key)
        {
            return PreparedSessions.FirstOrDefault(pair => pair.Key == key);
        }

        public virtual string PrepareNewSession(params ISessionAttribute[] attributes)
        {
            string sessionKey = CreateSessionKey();
            PreparedSessions.TryAdd(sessionKey, attributes);
            return sessionKey;
        }

        public virtual async Task<SessionSocket> JoinSession(HttpContext context, string sessionType, string sessionKey)
        {
            SocketSession targetSession = GetSessionByKey(sessionKey);
            if (targetSession == null)
            {
                targetSession = this.CreateSession(sessionType, sessionKey);
            }

            WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();

            SessionSocket sessionSocket = targetSession.AddWebSocket(socket);


            return sessionSocket;
        }

        protected virtual SocketSession CreateSession(string sessionType, string sessionKey)
        {

            KeyValuePair<string, ISessionAttribute[]> preparedSession = GetPreparedSession(sessionKey);
            if (preparedSession.Equals(default(KeyValuePair<string, ISessionAttribute[]>)))
            {
                throw new Exception("Unknown Session: " + sessionKey);
            }

            SocketSession targetSession = new SocketSession();
            targetSession.SessionKey = sessionKey;
            targetSession.SessionType = SocketSession.GetSessionType(sessionType);
            Sessions.TryAdd(sessionKey, targetSession);
            targetSession.SetAttributes(preparedSession.Value);

            PreparedSessions.TryRemove(preparedSession);

            return targetSession;
        }

        public virtual async Task EndSession(string sessionKey)
        {
            SocketSession session;
            Sessions.TryRemove(sessionKey, out session);

            await session.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the Session Service.", CancellationToken.None);

            KeyValuePair<string, ISessionAttribute[]> preparedSession = GetPreparedSession(sessionKey);
            if (!preparedSession.Equals(default(KeyValuePair<string, ISessionAttribute[]>)))
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
            SocketSession socketSession = this.GetSessionByKey(sessionKey);
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

            SocketSession socketSession = this.GetSessionByKey(sessionKey);
            if (socketSession == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            foreach (var pair in socketSession.GetSockets())
            {
                if (!socketIds.Contains(pair.Key) && pair.Value.Socket.State == WebSocketState.Open)
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
            SocketSession socketSession = this.GetSessionByKey(sessionKey);
            if (socketSession == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            foreach (var pair in socketSession.GetSockets())
            {
                if (!sockets.Contains(pair.Value.Socket) && pair.Value.Socket.State == WebSocketState.Open)
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
                SocketSession socketSession = this.GetSessionByKey(sessionKey);
                if (socketSession == null)
                {
                    throw new Exception("Unknown Session requested: " + sessionKey);
                }

                foreach (var pair in socketSession.GetSockets())
                {
                    if (!socketIds.Contains(pair.Key) && pair.Value.Socket.State == WebSocketState.Open)
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
                SocketSession socketSession = this.GetSessionByKey(sessionKey);
                if (socketSession == null)
                {
                    throw new Exception("Unknown Session requested: " + sessionKey);
                }

                foreach (var pair in socketSession.GetSockets())
                {
                    if (!sockets.Contains(pair.Value.Socket) && pair.Value.Socket.State == WebSocketState.Open)
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
            SocketSession socketSession = this.GetSessionByKey(sessionKey);
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
            SocketSession socketSession = this.GetSessionByKey(sessionKey);
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
            SocketSession socketSession = this.GetSessionByKey(sessionKey);
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

        public virtual async Task ReceiveMessage(WebSocket socket, WebSocketReceiveResult result, byte[] buffer)
        {
            // intentionally blank
            // you don't have to do anything with messages you get from the client
            // but if you need to, you can override this function. Otherwise it's
            // just an empty execution.
        }

        private static string CreateSessionKey()
        {
            return GenerateRandomString(12);
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
