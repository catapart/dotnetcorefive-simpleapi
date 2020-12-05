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

namespace SimpleAPI_NetCore50.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticateController : ControllerBase
    {
        private readonly UserManager<Authentication.Account> accountManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly IConfiguration Configuration;

        public AuthenticateController(UserManager<Authentication.Account> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration)
        {
            this.accountManager = userManager;
            this.roleManager = roleManager;
            Configuration = configuration;
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] Schemas.LoginModel model)
        {
            Authentication.Account account = await accountManager.FindByNameAsync(model.Email);
            if (account != null && await accountManager.CheckPasswordAsync(account, model.Password))
            {
                IList<string> accountRoles = await accountManager.GetRolesAsync(account);

                List<Claim> authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Email, account.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                };

                foreach (string role in accountRoles)
                {
                    authClaims.Add(new Claim(ClaimTypes.Role, role));
                }

                SymmetricSecurityKey authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JWT:PrivateKey"]));

                JwtSecurityToken token = new JwtSecurityToken(
                    issuer: Configuration["JWT:ValidIssuer"],
                    audience: Configuration["JWT:ValidAudience"],
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
        public async Task<IActionResult> Register([FromBody] Schemas.RegisterModel model)
        {
            Authentication.Account accountExists = await accountManager.FindByNameAsync(model.Email);
            if (accountExists != null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new Schemas.AuthResponse { Status = "Error", Message = "Account already exists!" });
            }

            Authentication.Account account = new Authentication.Account()
            {
                Email = model.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Email
            };
            IdentityResult result = await accountManager.CreateAsync(account, model.Password);
            if (!result.Succeeded)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new Schemas.AuthResponse { Status = "Error", Message = "Account creation failed! Please check request details and try again." });
            }

            return Ok(new Schemas.AuthResponse { Status = "Success", Message = "Account created successfully!" });
        }

        [HttpPost]
        [Route("register-admin")]
        public async Task<IActionResult> RegisterAdmin([FromBody] Schemas.RegisterModel model)
        {
            Authentication.Account accountExists = await accountManager.FindByNameAsync(model.Email);
            if (accountExists != null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new Schemas.AuthResponse { Status = "Error", Message = "Account already exists!" });
            }

            Authentication.Account account = new Authentication.Account()
            {
                Email = model.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Email
            };
            IdentityResult result = await accountManager.CreateAsync(account, model.Password);
            if (!result.Succeeded)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new Schemas.AuthResponse { Status = "Error", Message = "Account creation failed! Please check request details and try again." });
            }

            if (!await roleManager.RoleExistsAsync(Authentication.AccountRoles.Admin))
            {
                await roleManager.CreateAsync(new IdentityRole(Authentication.AccountRoles.Admin));
            }
            if (!await roleManager.RoleExistsAsync(Authentication.AccountRoles.Moderater))
            {
                await roleManager.CreateAsync(new IdentityRole(Authentication.AccountRoles.Moderater));
            }
            if (!await roleManager.RoleExistsAsync(Authentication.AccountRoles.ConsumerAdmin))
            {
                await roleManager.CreateAsync(new IdentityRole(Authentication.AccountRoles.ConsumerAdmin));
            }
            if (!await roleManager.RoleExistsAsync(Authentication.AccountRoles.Consumer))
            {
                await roleManager.CreateAsync(new IdentityRole(Authentication.AccountRoles.Consumer));
            }
            if (!await roleManager.RoleExistsAsync(Authentication.AccountRoles.Guest))
            {
                await roleManager.CreateAsync(new IdentityRole(Authentication.AccountRoles.Guest));
            }

            if (await roleManager.RoleExistsAsync(Authentication.AccountRoles.Admin))
            {
                await accountManager.AddToRoleAsync(account, Authentication.AccountRoles.Admin);
            }

            return Ok(new Schemas.AuthResponse { Status = "Success", Message = "Account created successfully!" });
        }
    }
}