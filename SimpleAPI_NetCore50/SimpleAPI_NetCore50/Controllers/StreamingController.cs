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
        private readonly SimpleApiContext DatabaseContext;
        private readonly ProgressSocketSessionService ProgressSessionService;

        private readonly long FileSizeLimit;
        private readonly string[] PermittedExtensions = { ".txt" };
        private readonly string QuarantinedFilePath;
        private readonly string TargetFilePath;

        private FormOptions DefaultFormOptions = new FormOptions();

        public StreamingController(ILogger<StreamingController> logger, IConfiguration configuration, SimpleApiContext context, ProgressSocketSessionService progressSessionService)
        {
            Logger = logger;
            AppConfig = configuration;
            DatabaseContext = context;
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
            FileMap fileMap =  await ProgressSessionService.StreamFileToServer(HttpContext.Request, ModelState, Logger, sessionKey);

            DatabaseContext.FileMaps.Add(fileMap);
            try
            {
                await DatabaseContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (DatabaseContext.FileMaps.Any(entry => entry.Id == fileMap.Id))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            return CreatedAtAction(nameof(UploadFile), new { filePath = fileMap.FilenameForDisplay });
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
