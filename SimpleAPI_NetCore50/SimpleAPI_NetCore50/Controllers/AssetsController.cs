using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace SimpleAPI_NetCore50.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AssetsController : Controller
    {
        public IWebHostEnvironment HostingEnvironment { get; }
        private readonly ILogger<AssetsController> Logger;
        private readonly IConfiguration AppConfig;
        private readonly Data.SimpleApiDBContext DatabaseContext;

        public AssetsController(IWebHostEnvironment hostingEnvironment, ILogger<AssetsController> logger, IConfiguration configuration, Data.SimpleApiDBContext context)
        {
            HostingEnvironment = hostingEnvironment;
            Logger = logger;
            AppConfig = configuration;
            DatabaseContext = context;
        }

        [HttpGet("{assetFullName}")]
        public async Task<ActionResult> Get(string assetFullName)
        {
            if(string.IsNullOrEmpty(assetFullName))
            {
                return NotFound();
            }
            
            // returning a Physical File will give a 500 error if it fails.
            // in this case, I just want to return that the file was not found;
            // the standard 404 message. So I'm wrapping these in a trycatch to
            // force a 404 in case the physical file can't be served.
            try
            {
                string storedFilesPath = Path.GetFullPath(AppConfig.GetValue<string>("FileUpload:StoredFilesPath"));
                string lookupName = WebUtility.HtmlEncode(assetFullName);
                string filePath;

                //check db first
                Models.FileMap fileMap = DatabaseContext.FileMaps.FirstOrDefault(entry => entry.FilenameForDisplay == lookupName);

                if (fileMap != null)
                {
                    filePath = Path.Combine(storedFilesPath, fileMap.FilenameOnDisk);

                    return PhysicalFile(filePath, fileMap.ContentType);
                }

                //if not found, check if it's just a file in our assets.
                filePath = Path.Combine(storedFilesPath, assetFullName);
                string contentType = GetStaticFileContentType(filePath);
                return PhysicalFile(filePath, contentType);
            }
            catch(Exception exception)
            {
                Logger.LogWarning(exception.Message);
                return NotFound();
            }
        }

        private string GetStaticFileContentType(string filePath)
        {
            FileExtensionContentTypeProvider provider = new FileExtensionContentTypeProvider();
            string contentType;
            if (!provider.TryGetContentType(filePath, out contentType))
            {
                contentType = "application/octet-stream";
            }

            return contentType;
        }
    }
}
