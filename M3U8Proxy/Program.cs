using AspNetCore.Proxy;
using M3U8Proxy.Middleware;
using Microsoft.AspNetCore.HttpOverrides;

const string myAllowSpecificOrigins = "corsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("m3u8", builder =>
    {
        builder.Cache();
        builder.Expire(TimeSpan.FromSeconds(5));
    });
});
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProxies();
builder.Services.AddHttpClient();
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(myAllowSpecificOrigins,
        policyBuilder =>
        {
            if (allowedOrigins != null)
                policyBuilder.WithOrigins(allowedOrigins)
                           .AllowAnyHeader()
                           .AllowAnyMethod()
                           .AllowCredentials();
            else
                policyBuilder.AllowAnyOrigin()
                           .AllowAnyHeader()
                           .AllowAnyMethod();
        });
});

var app = builder.Build();

app.UseCors(myAllowSpecificOrigins);
app.UseMiddleware<CacheControlMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseRouting();
app.UseOutputCache();
app.MapGet("/hello", async context => { await context.Response.WriteAsync("Hello, Bitches! v1.10"); });
app.UseAuthentication();
app.MapControllers();
app.Run();