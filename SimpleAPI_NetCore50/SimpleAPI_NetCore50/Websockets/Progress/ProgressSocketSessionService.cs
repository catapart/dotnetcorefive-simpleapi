using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace SimpleAPI_NetCore50.Websockets
{
    public class ProgressSocketSessionService : WebsocketSessionService
    {
        public readonly long FileSizeLimit;
        public readonly string[] PermittedExtensions = { ".txt" };
        public readonly string QuarantinedFilePath;
        public readonly string FileStoragePath;

        public FormOptions DefaultFormOptions = new FormOptions();

        // Overrides
        public ProgressSocketSessionService(IConfiguration configuration, Services.FileService fileService) : base(configuration, fileService)
        {

            FileSizeLimit = this.AppConfig.GetValue<long>("FileUpload:FileSizeLimit");
            FileStoragePath = AppConfig.GetValue<string>("FileUpload:StoredFilesPath");
            QuarantinedFilePath = AppConfig.GetValue<string>("FileUpload:QuarantinedFilePath");
        }

        public async override Task<SessionSocket> JoinSession(HttpContext context, string sessionType, string sessionKey)
        {
            WebsocketSession targetSession = GetSessionByKey(sessionKey);
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
            targetSession.SetAttribute("hostId", sessionSocket.Token.SocketId);

            // alert requester of their own token
            Models.SocketToken hostToken = sessionSocket.Token;
            Schemas.SocketSessionMessageResponse response = new Schemas.SocketSessionMessageResponse()
            {
                MessageType = Schemas.SocketSessionMessageType.Greeting,
                Message = System.Text.Json.JsonSerializer.Serialize(new { SessionKey = sessionKey, HostToken = hostToken, Token = sessionSocket.Token })
            };
            SendMessage(sessionSocket, response);

            return sessionSocket;
        }

        public async override Task ReceiveMessage(string sessionKey, SessionSocket sessionSocket, WebSocketReceiveResult result, byte[] buffer)
        {
            Schemas.SocketError error = new Schemas.SocketError(){ ErrorCode = "WP_001", Message = "Progress Sessions do not expect messages from the client." };
            SendMessage(sessionSocket.Socket, error);
        }

        // Custom functionality
        public async Task<Models.FileMap> StreamFileToServer(HttpRequest request, ModelStateDictionary modelState, ILogger logger,  string sessionKey)
        {
            return await this.FileService.StreamFileToDiskWithProgress(request, modelState, logger, this, sessionKey);
        }

        public async Task UpdateProgress(string sessionKey, int value, int total = -1, string stepId = "")
        {
            Schemas.ProgressResponse response = new Schemas.ProgressResponse()
            {
                SessionKey = sessionKey,
                UnitsCompleted = value,
                UnitTotal = total,
                StepID = stepId
            };

            Schemas.SocketSessionUpdate[] updates = new Schemas.SocketSessionUpdate[]
            {
                new Schemas.SocketSessionUpdate()
                {
                    Status = "progress",
                    Progress = response
                }
            };

            WebsocketSession socketSession = GetSessionByKey(sessionKey);
            string hostId = socketSession.GetAttributeValue<string>("hostId");
            Schemas.SocketSessionMessageResponse updateResponse = new Schemas.SocketSessionMessageResponse()
            {
                MessageType = Schemas.SocketSessionMessageType.StatusUpdates,
                Message = System.Text.Json.JsonSerializer.Serialize(updates)
            };
            SendMessage(sessionKey, hostId, updateResponse);
        }

        public void CancelUpload(string sessionKey)
        {
            this.FileService.CancelUpload(this, sessionKey);
        }
    }
}
