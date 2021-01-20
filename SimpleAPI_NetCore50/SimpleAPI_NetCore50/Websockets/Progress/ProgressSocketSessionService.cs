using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using SimpleAPI_NetCore50.Models;

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

        public async override Task<WebsocketSessionPeer> JoinSession(HttpContext context, string sessionType, string sessionKey)
        {
            WebsocketSession targetSession = GetSessionByKey(sessionKey);
            if (targetSession == null)
            {
                targetSession = this.CreateSession(sessionType, sessionKey);
            }

            if(targetSession.GetPeers().Count > 0)
            {
                throw new Exception("Unable to join Progress session that already has a subscriber.");
            }

            WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();

            WebsocketSessionPeer peer = targetSession.AddPeer(socket);
            targetSession.SetAttribute("hostId", peer.Token.PeerId);

            // alert requester of their own token
            WebsocketSessionPeerToken hostToken = peer.Token;
            WebsocketSessionGreeting greeting = new WebsocketSessionGreeting { SessionKey = sessionKey, HostToken = hostToken, Token = peer.Token };
            WebsocketSessionMessageResponse greetingResponse = CreateWebsocketResponseMessage(WebsocketSessionMessageType.Greeting, greeting);
            SendMessage(peer, greetingResponse);

            return peer;
        }

        public async override Task ReceiveMessage(string sessionKey, WebsocketSessionPeer peer, WebSocketReceiveResult result, byte[] buffer)
        {
            WebsocketSessionError error = new WebsocketSessionError{ ErrorCode = "WP_001", Message = "Progress Sessions do not expect messages from the client." };
            SendMessage(peer.Socket, error);
        }

        // Custom functionality
        public async Task<Models.FileMap> StreamFileToServer(HttpRequest request, ModelStateDictionary modelState, ILogger logger,  string sessionKey)
        {
            return await this.FileService.StreamFileToDiskWithProgress(request, modelState, logger, this, sessionKey);
        }

        public async Task UpdateProgress(string sessionKey, int value, int total = -1, string stepId = "")
        {
            WebsocketSessionFileProgress response = new WebsocketSessionFileProgress
            {
                SessionKey = sessionKey,
                UnitsCompleted = value,
                UnitTotal = total,
                StepID = stepId
            };

            WebsocketSessionUpdate[] updates = CreateProgressUpdates(WebsocketSessionUpdateStatus.Progress, response);
            WebsocketSession session = GetSessionByKey(sessionKey);
            string hostId = session.GetAttributeValue<string>("hostId");
            WebsocketSessionMessageResponse updateResponse = CreateWebsocketResponseMessage(WebsocketSessionMessageType.StatusUpdates, updates);
            SendMessage(sessionKey, hostId, updateResponse);
        }
        protected static WebsocketSessionUpdate[] CreateProgressUpdates(WebsocketSessionUpdateStatus status, WebsocketSessionFileProgress progress)
        {
            WebsocketSessionUpdate[] updates = new WebsocketSessionUpdate[]{
                new WebsocketSessionUpdate
                {
                    Status = GetSessionUpdateStatusString(status),
                    Progress = progress
                }
            };
            return updates;
        }

        public void CancelUpload(string sessionKey)
        {
            this.FileService.CancelUpload(this, sessionKey);
        }
    }
}
