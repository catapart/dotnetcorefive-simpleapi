using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Text;

namespace SimpleAPI_NetCore50.Websockets
{
    public class SocketSessionService
    {
        private ConcurrentDictionary<string, SocketSession> Sessions = new ConcurrentDictionary<string, SocketSession>();
        private List<string> PreparedKeys = new List<string>();

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

        public virtual string PrepareNewSession()
        {
            string sessionKey = CreateSessionKey();
            PreparedKeys.Add(sessionKey);
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
            if (PreparedKeys.IndexOf(sessionKey) == -1)
            {
                throw new Exception("Unknown Session: " + sessionKey);
            }
            SocketSession targetSession = new SocketSession();
            targetSession.SessionKey = sessionKey;
            targetSession.SessionType = SocketSession.GetSessionType(sessionType);
            Sessions.TryAdd(sessionKey, targetSession);
            PreparedKeys.Remove(sessionKey);

            return targetSession;
        }

        public virtual async Task EndSession(string sessionKey)
        {
            SocketSession session;
            Sessions.TryRemove(sessionKey, out session);

            await session.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the Session Service.", CancellationToken.None);
            if(PreparedKeys.IndexOf(sessionKey) != -1)
            {
                PreparedKeys.Remove(sessionKey);
            }
        }

        public async Task SendMessage(SessionSocket sessionSocket, string message)
        {
            await SendMessage(sessionSocket.Socket, message);
        }
        public async Task SendMessage(string sessionKey, string socketId, string message)
        {
            SocketSession socketSession = this.GetSessionByKey(sessionKey);
            if(socketSession == null)
            {
                throw new Exception("Unknown Session requested: " + sessionKey);
            }

            SessionSocket sessionSocket = socketSession.GetSocketById(socketId);
            if(sessionSocket == null)
            {
                throw new Exception("Unknown Socket requested: " + socketId);
            }

            await SendMessage(sessionSocket.Socket, message);
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

        public virtual async Task ReceiveMessage(WebSocket socket, WebSocketReceiveResult result, byte[] buffer)
        {

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
