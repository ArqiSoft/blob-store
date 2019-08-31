using Collector.Serilog.Enrichers.Assembly;
using MassTransit;
using MassTransit.RabbitMqTransport;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Sds.MassTransit.Observers;
using Sds.Storage.Blob.Core;
using Sds.Storage.Blob.GridFs;
using Sds.Storage.Blob.WebApi.Converters;
using Sds.Storage.Blob.WebApi.Middlewares;
using Sds.Storage.Blob.WebApi.Settings;
using Sds.Storage.Blob.WebApi.Swagger;
using Serilog;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Sds.Storage.Blob.WebApi
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            TypeDescriptor.AddAttributes(typeof(long), new TypeConverterAttribute(typeof(EnvironmentInt64Converter)));

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();

            Log.Logger = new LoggerConfiguration()
                .Enrich.With<SourceSystemInformationalVersionEnricher<Sds.Blob.Storage.WebApi.VersionController>>()
                .ReadFrom.Configuration(Configuration)
                .Enrich.FromLogContext()
                .CreateLogger();
        }

        public IConfigurationRoot Configuration { get; }

        private IServiceProvider Container { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            var mongoConnectionString = Environment.ExpandEnvironmentVariables(Configuration["OsdrConnectionSettings:ConnectionString"]);
            var mongoUrl = new MongoUrl(mongoConnectionString);
            Log.Information($"Connecting to MongoDB {mongoConnectionString}");
            services.AddTransient<IBlobStorage, GridFsStorage>(x => new GridFsStorage(x.GetService<IMongoDatabase>()));
            services.AddSingleton(new MongoClient(mongoUrl));
            services.AddSingleton(service => service.GetService<MongoClient>().GetDatabase(mongoUrl.DatabaseName));

            services.AddSingleton<IConfiguration>(Configuration);

            services.AddSingleton(context => Bus.Factory.CreateUsingRabbitMq(x =>
            {
                IRabbitMqHost host = x.Host(new Uri(Environment.ExpandEnvironmentVariables(Configuration["RabbitMq:ConnectionString"])), h => { });

                x.UseSerilog();
            }));

            //Register the Swagger generator, defining one or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "Blob Storage API", Version = "v1", Description = @"
                    The Blob Storage API allows you to upload or download files from OSDR.
                    For example, you can upload a MOL file, and then download the processed result. You can:
                    * Post a new file, with POST api/blobs/{bucket}
                    * Download an existing file with GET api/blobs/{bucket}/{id}
                    * Delete a blob, with DELETE api/blobs/{bucket}/{id}
                    You can also get the current version of this api with GET api/version" });

                //Set the comments path for the swagger json and ui.
                var basePath = PlatformServices.Default.Application.ApplicationBasePath;
                var xmlPath = Path.Combine(basePath, "Sds.Storage.Blob.WebApi.xml");
                c.IncludeXmlComments(xmlPath);
                c.DocumentFilter<LowercaseDocumentFilter>();
                c.AddSecurityDefinition("Bearer", new ApiKeyScheme()
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = "header",
                    Type = "apiKey"
                });
                c.OperationFilter<SecurityRequirementsOperationFilter>();
                c.OperationFilter<AddUploadFileParameter>();
            });
            var authorityUrl = Environment.ExpandEnvironmentVariables(Configuration["IdentityServer:Authority"]);
            Log.Information($"Identity server: {authorityUrl}");

            services.Configure<RequestSizeSettings>(Configuration.GetSection("BlobsUploadSettings"));

            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(cfg =>
            {
                cfg.Authority = authorityUrl;
                cfg.IncludeErrorDetails = true;
                cfg.RequireHttpsMetadata = false;
                cfg.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidIssuer = authorityUrl,
                    ValidateLifetime = true
                };

                cfg.Events = new JwtBearerEvents()
                {
                    OnAuthenticationFailed = c =>
                    {
                        c.NoResult();
                        c.Response.StatusCode = 401;
                        c.Response.ContentType = "text/plain";
                        return c.Response.WriteAsync(c.Exception.ToString());
                    },

                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        // If the request is for our hub...
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            // Read the token out of the query string
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });
            services.AddMvc()
                .AddJsonOptions(opt => opt.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented);

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IApplicationLifetime appLifetime)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();
            loggerFactory.AddSerilog();
            app.UseBlobStorageMiddleware();
            app.UseStaticFiles();
            app.UseAuthentication();

            // Enable middle-ware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger(c => c.PreSerializeFilters.Add((d, r) =>
            {
                string basePath = Environment.GetEnvironmentVariable("SWAGGER_BASEPATH");
                if (!string.IsNullOrEmpty(basePath))
                    d.BasePath = basePath;
            }));

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("../api-docs/swagger.json", "Blob Storage API V1");
                c.SwaggerEndpoint("v1/swagger.json", "Blob Storage API DEV");
            });

            app.UseCors(
                 builder => builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());

            app.UseMvc();

            var blobSettings = app.ApplicationServices.GetService<IOptions<RequestSizeSettings>>().Value;
            Log.Information($"Maximum size for blob to upload: {blobSettings.MaxRequestSize} bytes");

            var busControl = app.ApplicationServices.GetService<IBusControl>();
            busControl.ConnectPublishObserver(new PublishObserver());
            busControl.ConnectConsumeObserver(new ConsumeObserver());

            busControl.Start();

            appLifetime.ApplicationStopping.Register(() => busControl.Stop());
        }
    }
}
