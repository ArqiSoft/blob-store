using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Sds.Storage.Blob.WebApi.Settings;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Sds.Storage.Blob.WebApi.Middlewares
{
    public class MaxRequestMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly BlobStorageSettings _settings;

        public MaxRequestMiddleware(RequestDelegate next, IOptions<BlobStorageSettings> settings)
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

            await _next(context);
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

            return app.UseMiddleware<MaxRequestMiddleware>();
        }
    }
}
