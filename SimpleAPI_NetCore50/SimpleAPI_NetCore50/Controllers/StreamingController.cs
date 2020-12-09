using System;
using SimpleAPI_NetCore50.Websockets;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimpleAPI_NetCore50.Data;
using SimpleAPI_NetCore50.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Text;

namespace SimpleAPI_NetCore50.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StreamingController : Controller
    {
        private readonly ILogger<StreamingController> Logger;
        private readonly IConfiguration AppConfig;
        private readonly Services.FileService FileService;
        private readonly ProgressSocketSessionService ProgressSessionService;

        private readonly long FileSizeLimit;
        private readonly string[] PermittedExtensions = { ".txt" };
        private readonly string QuarantinedFilePath;
        private readonly string TargetFilePath;

        private FormOptions DefaultFormOptions = new FormOptions();

        public StreamingController(ILogger<StreamingController> logger, IConfiguration configuration, Services.FileService fileService,  ProgressSocketSessionService progressSessionService)
        {
            Logger = logger;
            AppConfig = configuration;
            FileService = fileService;
            ProgressSessionService = progressSessionService;

            FileSizeLimit = AppConfig.GetValue<long>("FileUpload:FileSizeLimit");
            TargetFilePath = AppConfig.GetValue<string>("FileUpload:StoredFilesPath");
            QuarantinedFilePath = AppConfig.GetValue<string>("FileUpload:QuarantinedFilePath");
        }

        [HttpPost("{sessionType}/{sessionKey}")]
        [DisableRequestSizeLimit]
        [Attributes.DisableFormValueModelBinding]
        public async Task<IActionResult> UploadFile(string sessionType, string sessionKey)
        {
            if (!IsMultipartContentType(Request.ContentType))
            {
                return Problem("Form content type must be 'multipart'");
            }

            var boundary = GetBoundary(MediaTypeHeaderValue.Parse(Request.ContentType), DefaultFormOptions.MultipartBoundaryLengthLimit);
            var reader = new MultipartReader(boundary, HttpContext.Request.Body);
            var section = await reader.ReadNextSectionAsync();

            while (section != null)
            {
                var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);

                if (hasContentDispositionHeader)
                {
                    // This check assumes that there's a file
                    // present without form data. If form data
                    // is present, this method immediately fails
                    // and returns the model error.
                    if (!HasFileContentDisposition(contentDisposition))
                    {
                        ModelState.AddModelError("File", $"The request couldn't be processed (Error 2).");
                        return BadRequest(ModelState);
                    }
                    else
                    {
                        // Don't trust the file name sent by the client. To display
                        // the file name, HTML-encode the value.
                        var trustedFileNameForDisplay = WebUtility.HtmlEncode(contentDisposition.FileName.Value);
                        var trustedFileNameForFileStorage = Path.GetRandomFileName();

                        if (!Directory.Exists(QuarantinedFilePath))
                        {
                            Directory.CreateDirectory(QuarantinedFilePath);
                        }
                        string quarantinedPath = Path.Combine(QuarantinedFilePath, trustedFileNameForFileStorage);

                        int totalBytes = -1;
                        SocketSession targetSession = ProgressSessionService.GetSessionByKey(sessionKey);
                        if(targetSession != null)
                        {
                            totalBytes = targetSession.GetAttributeValue<int>("unitTotal");
                        }

                        FileService.Init(ProgressSessionService, sessionKey, totalBytes);
                        await FileService.SaveFileToDisk(section, contentDisposition, ModelState, quarantinedPath, PermittedExtensions, FileSizeLimit);

                        if (!ModelState.IsValid)
                        {
                            return BadRequest(ModelState);
                        }

                        if (!Directory.Exists(TargetFilePath))
                        {
                            Directory.CreateDirectory(TargetFilePath);
                        }

                        System.IO.File.Move(quarantinedPath, Path.Combine(TargetFilePath, trustedFileNameForFileStorage));
                        Logger.LogInformation(
                                "Uploaded file '{TrustedFileNameForDisplay}' saved to " +
                                "'{TargetFilePath}' as {TrustedFileNameForFileStorage}",
                                trustedFileNameForDisplay, TargetFilePath,
                                trustedFileNameForFileStorage);
                    }
                }

                // Drain any remaining section body that hasn't been consumed and
                // read the headers for the next section.
                section = await reader.ReadNextSectionAsync();
            }

            return Created(nameof(StreamingController), null);
        }

        private static bool IsMultipartContentType(string contentType)
        {
            return !string.IsNullOrEmpty(contentType) && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
        // The spec at https://tools.ietf.org/html/rfc2046#section-5.1 states that 70 characters is a reasonable limit.
        private static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
        {
            var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;

            if (string.IsNullOrWhiteSpace(boundary))
            {
                throw new InvalidDataException("Missing content-type boundary.");
            }

            if (boundary.Length > lengthLimit)
            {
                throw new InvalidDataException($"Multipart boundary length limit {lengthLimit} exceeded.");
            }

            return boundary;
        }
        private static bool HasFileContentDisposition(ContentDispositionHeaderValue contentDisposition)
        {
            // Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
            return contentDisposition != null
                && contentDisposition.DispositionType.Equals("form-data")
                && (!string.IsNullOrEmpty(contentDisposition.FileName.Value)
                    || !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value));
        }

        private static Encoding GetEncoding(MultipartSection section)
        {
            var hasMediaTypeHeader = MediaTypeHeaderValue.TryParse(section.ContentType, out var mediaType);

            // UTF-7 is insecure and shouldn't be honored. UTF-8 succeeds in 
            // most cases.
            if (!hasMediaTypeHeader || Encoding.UTF7.Equals(mediaType.Encoding))
            {
                return Encoding.UTF8;
            }

            return mediaType.Encoding;
        }
    }
}
