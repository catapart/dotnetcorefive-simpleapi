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
    public class ProgressSocketSessionService : SocketSessionService
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

            await SendMessageToAll(sessionKey, response);
        }
    }
}
