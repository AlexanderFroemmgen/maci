using AutoMapper;
using Backend.Config;
using Backend.Data.Persistence;
using Backend.WorkerHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.IO;
using System.Linq;

namespace Backend
{
    public class Startup
    {
        private readonly IHostingEnvironment _env;

        public Startup(IHostingEnvironment env)
        {
            _env = env;
             var builder = new ConfigurationBuilder()
                 .SetBasePath(env.ContentRootPath)
                 .AddJsonFile("appsettings.json", true, true)
                 .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true)
                 .AddEnvironmentVariables();
             Configuration = builder.Build();

            MapperConfiguration = new MapperConfiguration(cfg => cfg.AddProfile(new AutoMapperProfile()));
        }

        public MapperConfiguration MapperConfiguration { get; }
        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(
                options =>
                {
                    options.AddPolicy("AllowAll",
                        p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().AllowCredentials());
                });

            if (Configuration.GetValue<string>("Backend:Server", "sqlite") == "sqlite") {
                var sqliteFileName = "maci_data.db";
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite("Data Source=./" + sqliteFileName));
            } else if (Configuration.GetValue<string>("Backend:Server", "sqlite") == "postgres") {
                // services.AddDbContext<ApplicationDbContext>(options =>  options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection")));
                services.AddDbContext<ApplicationDbContext>(options =>  options.UseNpgsql(Configuration.GetValue<string>("Backend:DefaultConnection")));
            }

            services.AddMvc().AddJsonOptions(options =>
            {
                // Use PascalCase for serializing JSON objects.
                options.SerializerSettings.ContractResolver = new DefaultContractResolver();
                options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
            });

            services.AddSingleton<IConfiguration>(Configuration); // Probably not the cleanest solution...
            services.AddSingleton(sp => MapperConfiguration.CreateMapper());
            services.AddSingleton<WorkerHostService>();
            services.AddSingleton<ScalingService>();
            services.AddMemoryCache();

            services.Configure<ExportOptions>(Configuration.GetSection("Export"));
            services.Configure<GitRemoteOptions>(Configuration.GetSection("Git:RemoteCredentials"));


            var DataLocation = Configuration.GetValue<string>("Backend:DataLocation");
            var directoryOptions = new DirectoryOptions()
            {
                DataLocation = DataLocation
            };

            EnsureDataLocationAvailable(DataLocation, directoryOptions);

            services.AddSingleton<DirectoryOptions>(directoryOptions);
        }

        private void EnsureDataLocationAvailable(string location, DirectoryOptions directoryOptions)
        {
            var di = new DirectoryInfo(location);
            if(!di.Exists)
            {
                di.Create();

                /* Copy existing examples to this location */
                directoryOptions.CopyDirectoryRecursively(new DirectoryInfo("AppData/ExperimentTemplates"), location + "/ExperimentTemplates");
                directoryOptions.CopyDirectoryRecursively(new DirectoryInfo("AppData/ExperimentFramework"), location + "/ExperimentFramework");
            }
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            var dbContext = app.ApplicationServices.GetService<ApplicationDbContext>();
            //dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();

            if(dbContext.Configuration.Count() == 0)
            {
                /* Prepare default configuration */
                dbContext.Configuration.Add(new Data.Persistence.Model.Configuration
                {
                    MaxIdleTimeSec = 60 * 60
                });
                dbContext.SaveChanges();
            }

            app.UseDefaultFiles();
            app.UseCors("AllowAll");

            app.UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true
            });

            app.UseMvc();
        }
    }
}
