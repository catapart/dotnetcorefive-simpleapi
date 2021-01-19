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

        public StreamingController(ILogger<StreamingController> logger, IConfiguration configuration, SimpleApiContext context, ProgressSocketSessionService progressSessionService)
        {
            Logger = logger;
            AppConfig = configuration;
            DatabaseContext = context;
            ProgressSessionService = progressSessionService;
        }

        [HttpPost("progress/{sessionKey}")]
        [DisableRequestSizeLimit]
        [Attributes.DisableFormValueModelBinding]
        public async Task<IActionResult> UploadFile(string sessionKey)
        {
            FileMap fileMap =  await ProgressSessionService.StreamFileToServer(HttpContext.Request, ModelState, Logger, sessionKey);
            fileMap.UnadjustedDisplayFilename = fileMap.FilenameForDisplay;

            FileMap[] existingMaps = DatabaseContext.FileMaps.Where(map => map.UnadjustedDisplayFilename == fileMap.UnadjustedDisplayFilename).ToArray();
            if(existingMaps == null || existingMaps.Length > 0)
            {
                fileMap.FilenameForDisplay = AddSuffixToFilename(fileMap.FilenameForDisplay, existingMaps.Length.ToString());
            }

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

        [HttpGet("progress/{sessionKey}/cancel")]
        public async Task<IActionResult> CancelUpload(string sessionKey)
        {
            ProgressSessionService.CancelUpload(sessionKey);

            return AcceptedAtAction(nameof(CancelUpload));
        }

        private string AddSuffixToFilename(string fileName, string suffix)
        {
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(baseName);
            stringBuilder.Append("(");
            stringBuilder.Append(suffix);
            stringBuilder.Append(")");
            stringBuilder.Append(extension);
            return stringBuilder.ToString();
        }
    }
}
