using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Web;

namespace M3U8Proxy.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _apiKey;
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
        "application/octet-stream"  // Some servers send this for ts files
    };

    private static readonly string[] ValidHlsExtensions =
    {
        ".m3u8",
        ".m3u",
        ".ts",    // MPEG transport stream
        ".aac",   // Audio segments
        ".key",   // Encryption keys
        ".vtt",   // WebVTT subtitles
        ".srt"    // SubRip subtitles
    };

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _next = next;
        _apiKey = configuration["ApiKey"]!;
        _httpClientFactory = httpClientFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add Cache-Control header for all routes
        context.Response.Headers.CacheControl = "public, max-age=15768000";

        // Handle both /proxy/ and /proxy/m3u8/ routes
        if (context.Request.Path.StartsWithSegments("/proxy") || context.Request.Path.StartsWithSegments("/proxy/m3u8"))
        {
            var pathValue = context.Request.Path.Value!;
            var urlPath = pathValue.StartsWith("/proxy/m3u8/") 
                ? pathValue.Substring("/proxy/m3u8/".Length)
                : pathValue.Substring("/proxy/".Length);
            
            if (string.IsNullOrEmpty(urlPath))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Invalid request",
                    details = "URL must be provided in the path"
                });
                return;
            }

            // Split URL and parameters
            var parts = urlPath.Split(new[] { '/' }, 2);
            var encodedUrl = parts[0];
            var paramsJson = parts.Length > 1 ? parts[1] : null;

            // Try decoding twice in case of double-encoding
            var url = HttpUtility.UrlDecode(encodedUrl);
            if (url.Contains("%"))
            {
                url = HttpUtility.UrlDecode(url);
            }

            // Parse parameters if they exist
            if (!string.IsNullOrEmpty(paramsJson))
            {
                try
                {
                    var decodedParams = HttpUtility.UrlDecode(paramsJson);
                    var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(decodedParams);
                    
                    context.Items["ProxyParameters"] = parameters;
                }
                catch
                {
                    // If JSON parsing fails, ignore the parameters
                }
            }

            // Check if it's a valid URL
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Invalid request",
                    details = "Invalid URL format",
                    url = url
                });
                return;
            }

            // Store the original URL in HttpContext.Items
            context.Items["OriginalUrl"] = url;

            // Check if URL has a valid HLS extension
            if (ValidHlsExtensions.Any(ext => uri.AbsolutePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            // If no valid extension, check content type
            try
            {
                using var client = _httpClientFactory.CreateClient();
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                
                if (context.Items["ProxyParameters"] is Dictionary<string, string> parameters &&
                    parameters.TryGetValue("referer", out var referer))
                {
                    request.Headers.Referrer = new Uri($"https://{referer}");
                }

                var response = await client.SendAsync(request);
                
                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType == null || !ValidHlsContentTypes.Contains(contentType.ToLower()))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = "Invalid request",
                        details = "URL must point to an HLS stream or segment"
                    });
                    return;
                }
            }
            catch
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Invalid request",
                    details = "Unable to verify URL content type"
                });
                return;
            }

            await _next(context);
            return;
        }

        // Rest of the API key validation code remains the same...
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