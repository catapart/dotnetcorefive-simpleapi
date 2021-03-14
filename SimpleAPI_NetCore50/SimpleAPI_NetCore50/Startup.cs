using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace SimpleAPI_NetCore50
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IWebHostEnvironment HostingEnvironment { get; }
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            HostingEnvironment = environment;
        }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddTransient<Websockets.WebsocketSession>(); // ephemeral sessions
            foreach (var type in Assembly.GetEntryAssembly().ExportedTypes)
            {
                // this loop won't register the base type; only implementers;
                if (type.GetTypeInfo().BaseType == typeof(Websockets.WebsocketSessionService))
                {
                    services.AddSingleton(type); // long-lived management services
                }

                // so we register the base type explicitly;
                services.AddSingleton<Websockets.WebsocketSessionService>();
            }

            services.AddSingleton<Services.FileService>();

            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
                    //builder.WithOrigins("http://example.com", "http://www.contoso.com");
                });
            });

            services.AddDbContext<Data.SimpleApiDBContext>(options => options.UseSqlServer(Configuration.GetConnectionString("development")));
            services.AddIdentity<Authentication.Account, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
            .AddEntityFrameworkStores<Data.SimpleApiDBContext>()
            .AddDefaultTokenProviders();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudience = Configuration["JWT:ValidAudience"],
                    ValidIssuer = Configuration["JWT:ValidIssuer"],
                    IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Configuration["JWT:PrivateKey"]))
                };
            });


            services.AddControllers()
            .AddJsonOptions(options =>
            {
                // prevent the api from converting all property names to lowercase;
                // this may cause direct issues with javascript interaction, but
                // it also affects how OpenAPI displays the properties. Very important
                // to get those casings correct in the API documentation.
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1.0.0",
                    Title = $"SimpleAPI_NetCore50 API v1",
                    Description = "v1 API",
                    //TermsOfService = new Uri("[your TOS]"),
                    Contact = new OpenApiContact
                    {
                        Name = "@catapart",
                        Email = "not@myrealemail.com"
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT",
                        Url = new Uri("https://github.com/catapart/dotnetcorefive-simpleapi/blob/main/LICENSE")
                    }
                });

                // this allows fine grain control over how specific parts of the
                // documentation is generated. Here, we're transforming enums from
                // integer lists into string lists.
                c.SchemaFilter<Filters.DescribeEnumsAsStringsFilter>();

                // add any custom schemas you want to show in OpenAPI, here. The MUST be classes (not structs)
                c.DocumentFilter<Filters.CustomModelDocumentFilter<Models.WebsocketSessionError>>();
                c.DocumentFilter<Filters.CustomModelDocumentFilter<Models.WebsocketSessionFileProgress>>();
                c.DocumentFilter<Filters.CustomModelDocumentFilter<Models.WebsocketSessionGreeting>>();
                c.DocumentFilter<Filters.CustomModelDocumentFilter<Models.WebsocketSessionMessageRequest>>();
                c.DocumentFilter<Filters.CustomModelDocumentFilter<Models.WebsocketSessionMessageResponse>>();
                c.DocumentFilter<Filters.CustomModelDocumentFilter<Models.WebsocketSessionMessageType>>();
                c.DocumentFilter<Filters.CustomModelDocumentFilter<Models.WebsocketSessionPeerToken>>();
                c.DocumentFilter<Filters.CustomModelDocumentFilter<Models.WebsocketSessionRequest>>();
                c.DocumentFilter<Filters.CustomModelDocumentFilter<Models.WebsocketSessionUpdate>>();
                c.DocumentFilter<Filters.CustomModelDocumentFilter<Models.WebsocketSessionUpdateStatus>>();

                // we don't need to do this, because the controller endpoint has an attribute
                // that defines the response; OpenAPI will generate the schema from there.
                //c.DocumentFilter<Filters.CustomModelDocumentFilter<Models.AuthResponse>>();

                // custom schemas shouldn't really be necessary for REST operations;
                // we're using them here because the WebsocketSession models do not
                // route through a controller, so OpenAPI doesn't automatically
                // provide those models.
            });

            //services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(x => x.MultipartBodyLengthLimit = 1_074_790_400);
            // this seemed necessary for uploading large data items, but the actual control of this is in
            // a hidden file at [.sln]/.vs/SimpleAPI_NetCore50/config/applicationhost.config;
            // In the "requestFiltering" node of the "security" node, there is a "reqeustLimits" node that has a "maxAllowedContentLength" attribute. 
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseExceptionHandler("/error-development");
                //app.UseDeveloperExceptionPage(); // this is also helpful for debugging if you don't have a client to display the errors;
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SimpleAPI_NetCore50 v1"));
            }
            else
            {
                app.UseExceptionHandler("/error");
            }
            var serviceScopeFactory = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>();
            var serviceProvider = serviceScopeFactory.CreateScope().ServiceProvider;

            app.UseWebSockets();
            app.Map("/messaging", (_app) => _app.UseMiddleware<Websockets.WebsocketSessionMiddleware>(serviceProvider.GetService<Websockets.WebsocketSessionService>()));
            app.Map("/progress", (_app) => _app.UseMiddleware<Websockets.ProgressSocketMiddleware>(serviceProvider.GetService<Websockets.ProgressSocketSessionService>()));
            // a second service isn't really necessary; this is just used to demonstrate how to add a
            // second/specialty websocket service, for more granular control.

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions()
            {
                OnPrepareResponse = context =>
                {
                    context.Context.Response.Headers.Add("Cache-Control", "no-cache, no-store");
                    context.Context.Response.Headers.Add("Expires", "-1");
                }
            });

            app.UseCors();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
