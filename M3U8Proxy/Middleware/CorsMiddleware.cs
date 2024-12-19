using Microsoft.AspNetCore.Http;

namespace M3U8Proxy.Middleware;

public class CorsMiddleware
{
    private readonly RequestDelegate _next;

    public CorsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Handle preflight OPTIONS request
        if (context.Request.Method == "OPTIONS")
        {
            // Add CORS headers
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", 
                "Origin, Range, Accept-Encoding, Referer, Cache-Control, X-Requested-With, Content-Type");
            context.Response.Headers.Add("Access-Control-Expose-Headers", 
                "Server, Content-Length, Content-Range, Date");
            context.Response.Headers.Add("Access-Control-Max-Age", "86400"); // 24 hours

            // Return 204 for OPTIONS requests
            context.Response.StatusCode = 204;
            return;
        }

        await _next(context);
    }
} 