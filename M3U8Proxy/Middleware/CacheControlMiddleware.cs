using Microsoft.AspNetCore.Http;

namespace M3U8Proxy.Middleware;

public class CacheControlMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly string[] CloudflareHeaders = 
    {
        "Cf-Cache-Status",
        "Cf-Ray",
        "Cf-Connecting-Ip",
        "Cf-Ipcountry",
        "Cf-Visitor",
        "Cf-Request-Id",
        "Cf-Worker",
        "Cf-Waf-Error-Id",
        "Cf-Pol-Decisions",
        "Cf-Bot-Score",
        "Cf-Bot-Management-Tag",
        "Cf-Challenge-Id",
        "Cf-Threat-Score"
    };

    public CacheControlMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Remove Cache-Control and Cloudflare headers from request
        context.Request.Headers.Remove("Cache-Control");
        foreach (var header in CloudflareHeaders)
        {
            context.Request.Headers.Remove(header);
        }

        // Remove cache and Cloudflare headers from response
        context.Response.OnStarting(() =>
        {
            if (!context.Response.HasStarted)
            {
                // Remove Cloudflare headers
                foreach (var header in CloudflareHeaders)
                {
                    context.Response.Headers.Remove(header);
                }

                // Add Cache-Control only for successful responses (2xx status codes)
                if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
                {
                    context.Response.Headers.Remove("Cache-Control");
                    context.Response.Headers.CacheControl = "public, max-age=86400";
                }
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
} 