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
    }
}
