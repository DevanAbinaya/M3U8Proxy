using System.Net.Http.Headers;

namespace M3U8Proxy.RequestHandler;

public static class SubtitleHandler
{
    private static readonly Dictionary<string, string> SubtitleMimeTypes = new()
    {
        { ".vtt", "text/vtt" },
        { ".srt", "application/x-subrip" },
        { ".ass", "text/x-ssa" },
        { ".ssa", "text/x-ssa" },
        { ".ttml", "application/ttml+xml" },
        { ".dfxp", "application/ttaf+xml" }
    };

    public static bool IsSubtitleFile(string url)
    {
        var extension = Path.GetExtension(url).ToLowerInvariant();
        return SubtitleMimeTypes.ContainsKey(extension);
    }

    public static void SetSubtitleContentType(HttpResponseMessage response, string url)
    {
        var extension = Path.GetExtension(url).ToLowerInvariant();
        if (SubtitleMimeTypes.TryGetValue(extension, out var mimeType))
        {
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            // Ensure proper charset for text-based subtitle files
            if (mimeType.StartsWith("text/"))
            {
                response.Content.Headers.ContentType.CharSet = "utf-8";
            }
        }
    }
}
