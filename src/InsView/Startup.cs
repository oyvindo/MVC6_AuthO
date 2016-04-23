using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Data.Entity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using InsView.Models;
using InsView.Properties;
using InsView.Services;
using Microsoft.AspNet.Authentication;
using Microsoft.AspNet.Authentication.Cookies;
using Microsoft.AspNet.Http;
using Microsoft.Extensions.PlatformAbstractions;
using WebApp.Properties;

namespace InsView
{
    public class Startup
    {
        public Startup(IHostingEnvironment env, IApplicationEnvironment app)
        {
            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(app.ApplicationBasePath)
                .AddJsonFile("config.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            if (env.IsDevelopment())
            {
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets();
            }

            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddWebEncoders();
            services.AddDataProtection();

            services.Configure<Auth0Settings>(Configuration.GetSection("Auth0"));
            services.Configure<AppSettings>(Configuration.GetSection("AppSettings"));

            services.Configure<SharedAuthenticationOptions>(options =>
            {
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            });

            var settings = Configuration.Get<Auth0Settings>("Auth0");

            services.UseAuth0(settings.Domain, settings.ClientId, settings.ClientSecret, settings.RedirectUri, notification =>
            {
                var identity = notification.AuthenticationTicket.Principal.Identity as ClaimsIdentity;

                // Optional: add custom claims.
                /* 
                if (identity.HasClaim(c => c.Type == "name"))
                    identity.AddClaim(new Claim(ClaimTypes.Name, identity.FindFirst("name").Value));
                identity.AddClaim(new Claim("tenant", "12345"));
                identity.AddClaim(new Claim("custom-claim", "custom-value"));
                */

                // Optional: store tokens in the user object so you can retrieve those later.
                /*
                if (!String.IsNullOrEmpty(notification.TokenEndpointResponse.AccessToken))
                    identity.AddClaim(new Claim("access_token", notification.TokenEndpointResponse.AccessToken));
                if (!String.IsNullOrEmpty(notification.TokenEndpointResponse.IdToken))
                    identity.AddClaim(new Claim("id_token", notification.TokenEndpointResponse.IdToken));
                if (!String.IsNullOrEmpty(notification.TokenEndpointResponse.RefreshToken))
                    identity.AddClaim(new Claim("refresh_token", notification.TokenEndpointResponse.RefreshToken)); 
                */
                return Task.FromResult(true);
            });

            // Add framework services.
            services.AddEntityFramework()
                .AddSqlServer()
                .AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(Configuration["Data:DefaultConnection:ConnectionString"]));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            // Add application services.
            services.AddTransient<IEmailSender, AuthMessageSender>();
            services.AddTransient<ISmsSender, AuthMessageSender>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");

                // For more details on creating database during deployment see http://go.microsoft.com/fwlink/?LinkID=615859
                try
                {
                    using (var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>()
                        .CreateScope())
                    {
                        serviceScope.ServiceProvider.GetService<ApplicationDbContext>()
                            .Database.Migrate();
                    }
                }
                catch
                {
                }
            }

            app.UseStaticFiles();

            app.UseCookieAuthentication(options =>
            {
                options.AutomaticAuthenticate = true;
                options.LoginPath = new PathString("/Account/Login");
            });

            app.UseAuth0();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        // Entry point for the application.
        public static void Main(string[] args) => WebApplication.Run<Startup>(args);
    }
}
