using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimpleAPI_NetCore50.Data;
using SimpleAPI_NetCore50.Models;
using System.Reflection;

namespace SimpleAPI_NetCore50.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApplicationController : Controller
    {
        private readonly SimpleApiContext DatabaseContext;

        public ApplicationController(SimpleApiContext context)
        {
            DatabaseContext = context;
        }

        // GET: api/Application
        [HttpGet("version")]
        public async Task<ActionResult> Version()
        {
            string appVersion = Assembly.GetEntryAssembly().GetName().Version.ToString();
            return await Task.FromResult(Ok(appVersion));
        }

        // GET: api/Application
        [HttpGet("bootstrap")]
        public async Task<ActionResult> Bootstrap()
        {
            return Ok();
        }
    }
}
