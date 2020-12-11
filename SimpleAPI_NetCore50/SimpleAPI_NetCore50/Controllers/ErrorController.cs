using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Diagnostics;

namespace SimpleAPI_NetCore50.Controllers
{
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ErrorController : Controller
    {
        [Route("/error")]
        public async Task<ActionResult> Error() => await Task.FromResult(Problem());

        [Route("/error-development")]
        public async Task<ActionResult> ErrorLocalDevelopment([FromServices] IWebHostEnvironment webHostEnvironment)
        {
            if (webHostEnvironment.EnvironmentName != "Development")
            {
                throw new InvalidOperationException("This shouldn't be invoked in non-development environments.");
            }

            ExceptionHandlerFeature context = (ExceptionHandlerFeature)HttpContext.Features.Get<IExceptionHandlerFeature>();
            if(context?.Path != null)
            {
                if(context.Path.StartsWith("/assets/"))
                {
                    int substringIndex = context.Path.LastIndexOf('/');
                    substringIndex = (substringIndex == -1) ? 0 : substringIndex;
                    return NotFound(context.Path.Substring(substringIndex));
                }
            }
            var exception = context?.Error;
            var code = 500;

            Response.StatusCode = code; // You can use HttpStatusCode enum instead


            return Problem(detail: context.Error.StackTrace, title: context.Error.Message);
        }
    }
}
