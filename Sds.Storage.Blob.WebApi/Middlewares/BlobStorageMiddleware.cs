using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Sds.Storage.Blob.WebApi.Settings;
using Serilog;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Sds.Storage.Blob.WebApi.Middlewares
{
    public class BlobStorageMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RequestSizeSettings _settings;

        public BlobStorageMiddleware(RequestDelegate next, IOptions<RequestSizeSettings> settings)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _settings = settings?.Value;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.ContentLength > _settings.MaxRequestSize)
            {
                context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                return;
            }

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                if (!context.Response.HasStarted)
                {
                    await context.Response.WriteAsync(ex.ToString());
                }
            }
        }
    }

    public static class BlobStorageMiddlewareExtensions
    {
        public static IApplicationBuilder UseBlobStorageMiddleware(this IApplicationBuilder app)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            return app.UseMiddleware<BlobStorageMiddleware>();
        }
    }
}
