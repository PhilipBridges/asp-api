using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TodoApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Mvc.Authorization;
using System.Security.Claims;
using Microsoft.IdentityModel.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace TodoApi
{
    public class Startup
    {
        private readonly IHostingEnvironment _environment;
        private readonly ILogger _logger;
        public Startup(IHostingEnvironment environment, IConfiguration configuration, ILogger<Startup> logger)
        {
            Configuration = configuration;
            _environment = environment;
            _logger = logger;

        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            IdentityModelEventSource.ShowPII = true;

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = _environment.IsDevelopment()
                  ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Name = "SimpleTalk.AuthCookieAspNetCore";
                options.LoginPath = "/Home/Login";
                options.LogoutPath = "/Home/Logout";
                options.EventsType = typeof(RevokeAuthenticationEvents);
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("test",
                    policy => policy.RequireClaim("access_token"));
            });

            services.Configure<CookiePolicyOptions>(options =>
            {
                options.MinimumSameSitePolicy = SameSiteMode.Strict;
                options.HttpOnly = HttpOnlyPolicy.None;
                options.Secure = _environment.IsDevelopment()
                    ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
                options.CheckConsentNeeded = context => true;
            });

            services.AddMemoryCache();

            services.AddScoped<RevokeAuthenticationEvents>();
            services.AddTransient<ITicketStore, InMemoryTicketStore>();

            services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>,
            ConfigureCookieAuthenticationOptions>();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "My API", Version = "v1" });
            });

            var connection = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=TodoDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";

            services.AddDbContext<TodoApiContext>(options =>
                        options.UseSqlServer(connection));

            services.AddCors(options =>
            {
            options.AddPolicy("CorsPolicy",
                builder => builder
                .WithOrigins("http://localhost:3000")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
            });

            services.AddMvc(options => options.Filters.Add(new AuthorizeFilter()))
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
                c.RoutePrefix = "swagger";
            });

            app.UseDeveloperExceptionPage();
            app.UseCookiePolicy();
            app.UseAuthentication();

            app.Use(async (context, next) =>
            {
                var principal = context.User as ClaimsPrincipal;
                var accessToken = principal?.Claims
                  .FirstOrDefault(c => c.Type == "access_token");

                if (accessToken != null)
                {
                    _logger.LogDebug(accessToken.Value);
                }

                await next();
            });

            app.UseCors("CorsPolicy");

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                  name: "default",
                  template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
