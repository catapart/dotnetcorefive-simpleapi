using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SimpleAPI_NetCore50.Controllers
{
    [Route("api/account")]
    [ApiController]
    public class AccountController : Controller
    {
        private readonly UserManager<Authentication.Account> AccountManager;
        private readonly RoleManager<IdentityRole> RoleManager;
        private readonly IConfiguration AppConfig;

        public AccountController(IConfiguration configuration, UserManager<Authentication.Account> userManager, RoleManager<IdentityRole> roleManager)
        {
            this.AccountManager = userManager;
            this.RoleManager = roleManager;
            AppConfig = configuration;
        }

        [HttpPost]
        [Route("login")]
        public async Task<ActionResult> Login([FromBody] Models.AuthRequest model)
        {
            Authentication.Account account = await AccountManager.FindByNameAsync(model.Email);
            if (account != null && await AccountManager.CheckPasswordAsync(account, model.Password))
            {
                IList<string> accountRoles = await AccountManager.GetRolesAsync(account);

                List<Claim> authClaims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Email, account.Email),
                        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    };

                foreach (string role in accountRoles)
                {
                    authClaims.Add(new Claim(ClaimTypes.Role, role));
                }

                SymmetricSecurityKey authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AppConfig["JWT:PrivateKey"]));

                JwtSecurityToken token = new JwtSecurityToken(
                    issuer: AppConfig["JWT:ValidIssuer"],
                    audience: AppConfig["JWT:ValidAudience"],
                    expires: DateTime.Now.AddHours(3),
                    claims: authClaims,
                    signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                    );

                return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), expiration = token.ValidTo });
            }
            return Unauthorized();
        }

        [HttpPost]
        [Route("register")]
        [ProducesResponseType(typeof(Models.AuthResponse), 200)]
        public async Task<ActionResult> Register([FromBody] Models.AuthRequest model)
        {
            Authentication.Account accountExists = await AccountManager.FindByNameAsync(model.Email);
            if (accountExists != null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new Models.AuthResponse { Status = "Error", Message = "Account already exists!" });
            }

            Authentication.Account account = new Authentication.Account()
            {
                Email = model.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Email
            };
            IdentityResult result = await AccountManager.CreateAsync(account, model.Password);
            if (!result.Succeeded)
            {
                string errorMessage = "Account creation failed! Please check request details and try again.";
                foreach (var error in result.Errors)
                {
                    errorMessage += Environment.NewLine;
                    errorMessage += "  - " + error.Description;
                }
                return StatusCode(StatusCodes.Status500InternalServerError, new Models.AuthResponse { Status = "Error", Message = errorMessage });
            }

            await this.SanitizeRole(model);

            if (await RoleManager.RoleExistsAsync(model.Role))
            {
                await AccountManager.AddToRoleAsync(account, model.Role);
            }

            return Ok(new Models.AuthResponse { Status = "Success", Message = "Account created successfully!" });
        }

        private async Task SanitizeRole(Models.AuthRequest model)
        {
            // make sure all of your roles actually exist
            if (!await RoleManager.RoleExistsAsync(Authentication.AccountRole.Admin))
            {
                await RoleManager.CreateAsync(new IdentityRole(Authentication.AccountRole.Admin));
            }
            if (!await RoleManager.RoleExistsAsync(Authentication.AccountRole.Moderater))
            {
                await RoleManager.CreateAsync(new IdentityRole(Authentication.AccountRole.Moderater));
            }
            if (!await RoleManager.RoleExistsAsync(Authentication.AccountRole.ConsumerAdmin))
            {
                await RoleManager.CreateAsync(new IdentityRole(Authentication.AccountRole.ConsumerAdmin));
            }
            if (!await RoleManager.RoleExistsAsync(Authentication.AccountRole.Consumer))
            {
                await RoleManager.CreateAsync(new IdentityRole(Authentication.AccountRole.Consumer));
            }
            if (!await RoleManager.RoleExistsAsync(Authentication.AccountRole.Guest))
            {
                await RoleManager.CreateAsync(new IdentityRole(Authentication.AccountRole.Guest));
            }

            // get the right one based on the string
            if(String.Equals(model.Role, ""))
            {
                model.Role = Authentication.AccountRole.Consumer;
            }
        }
    }
}