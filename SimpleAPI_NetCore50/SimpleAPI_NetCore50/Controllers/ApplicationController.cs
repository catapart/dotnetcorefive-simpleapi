using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SimpleAPI_NetCore50.Data;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

namespace SimpleAPI_NetCore50.Controllers
{
    [Route("api/app")]
    [ApiController]
    public class ApplicationController : Controller
    {
        private readonly SimpleApiDBContext DatabaseContext;
        private readonly ILogger<ApplicationController> Logger;

        public ApplicationController(ILogger<ApplicationController> logger, SimpleApiDBContext context)
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
        // GET: api/Application
        [HttpGet("secure-data")]
        [Authorize]
        public async Task<ActionResult> SecureData()
        {
            string appVersion = "You should only see this if you provided an authorized token.";
            return await Task.FromResult(Ok(appVersion));
        }
    }
}
