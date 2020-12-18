using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SimpleAPI_NetCore50.Data;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace SimpleAPI_NetCore50.Controllers
{
    [Route("api/app")]
    [ApiController]
    public class ApplicationController : Controller
    {
        private readonly SimpleApiContext DatabaseContext;
        private readonly ILogger<ApplicationController> Logger;

        public ApplicationController(ILogger<ApplicationController> logger, SimpleApiContext context)
        {
            DatabaseContext = context;
            Logger = logger;
        }

        // GET: api/Application
        [HttpGet("version")]
        public async Task<ActionResult> Version()
        {
            string appVersion = Assembly.GetEntryAssembly().GetName().Version.ToString();
            return await Task.FromResult(Ok(appVersion));
        }
    }
}
