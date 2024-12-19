using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Web;

namespace M3U8Proxy.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string? _apiKey;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string API_KEY_HEADER_NAME = "X-API-Key";
    private const string API_KEY_QUERY_NAME = "api_key";
    private static readonly string[] ValidHlsContentTypes = 
    { 
        "application/x-mpegurl",
        "application/vnd.apple.mpegurl",
        "audio/mpegurl",
        "audio/x-mpegurl",
        "application/m3u8",
        "video/mp2t",           // For .ts files
        "application/octet-stream",  // Some servers send this for ts files
        "image/jpeg",           // For JPEG segments
        "image/jpg"            // Alternative JPEG content type
    };

    private static readonly string[] ValidHlsExtensions =
    {
        ".m3u8",
        ".m3u",
        ".ts",    // MPEG transport stream
        ".aac",   // Audio segments
        ".key",   // Encryption keys
        ".vtt",   // WebVTT subtitles
        ".srt",   // SubRip subtitles
        ".jpg"    // JPEG segments (some providers use this)
    };

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _next = next;
        _apiKey = configuration["ApiKey"];
        _httpClientFactory = httpClientFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip API key validation for OPTIONS requests
        if (context.Request.Method == "OPTIONS")
        {
            await _next(context);
            return;
        }

        // Skip API key validation if no API key is configured
        if (string.IsNullOrEmpty(_apiKey))
        {
            await _next(context);
            return;
        }

        // Skip API key validation for proxy endpoints
        if (context.Request.Path.StartsWithSegments("/proxy") || 
            context.Request.Path.StartsWithSegments("/proxy/m3u8") ||
            context.Request.Path.StartsWithSegments("/video"))
        {
            await _next(context);
            return;
        }

        // Rest of the middleware remains unchanged
        if (context.Request.Headers.TryGetValue(API_KEY_HEADER_NAME, out var headerApiKey) && 
            _apiKey.Equals(headerApiKey))
        {
            await _next(context);
            return;
        }

        var queryApiKey = context.Request.Query[API_KEY_QUERY_NAME].ToString();
        if (!string.IsNullOrEmpty(queryApiKey) && _apiKey.Equals(queryApiKey))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { 
            message = "API Key is missing or invalid",
            details = "Please provide a valid API key either in the X-API-Key header or as an api_key query parameter"
        });
    }
} 