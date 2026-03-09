using System.Reflection;
using Microsoft.AspNetCore.Http;
using SmtpServer.Dashboard.Configuration;

namespace SmtpServer.Dashboard.Middleware;

public class DashboardStaticFilesMiddleware(
    RequestDelegate next,
    FakeDashboardOptions options)
{
    private static readonly Assembly Assembly = typeof(DashboardStaticFilesMiddleware).Assembly;
    private static readonly string Prefix = "SmtpServer.Dashboard.Assets.";

    private static readonly Dictionary<string, string> ContentTypes = new()
    {
        [".html"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "application/javascript; charset=utf-8",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (!path.StartsWith(options.PathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var relativePath = path[options.PathPrefix.Length..].TrimStart('/');

        // Serve index.html for the root and message detail routes
        if (string.IsNullOrEmpty(relativePath) || relativePath.StartsWith("message/"))
        {
            await ServeIndex(context);
            return;
        }

        // Serve static assets
        if (relativePath.StartsWith("assets/"))
        {
            var resourcePath = relativePath["assets/".Length..].Replace('/', '.');
            await ServeEmbeddedResource(context, resourcePath);
            return;
        }

        await next(context);
    }

    private async Task ServeIndex(HttpContext context)
    {
        using var stream = Assembly.GetManifestResourceStream($"{Prefix}index.html");
        if (stream is null) { context.Response.StatusCode = 404; return; }

        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();
        html = html.Replace("{{TITLE}}", options.Title)
                   .Replace("{{PATH_PREFIX}}", options.PathPrefix.TrimEnd('/'));

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html);
    }

    private async Task ServeEmbeddedResource(HttpContext context, string resourcePath)
    {
        var fullPath = $"{Prefix}{resourcePath}";
        using var stream = Assembly.GetManifestResourceStream(fullPath);
        if (stream is null) { context.Response.StatusCode = 404; return; }

        var ext = Path.GetExtension(resourcePath);
        context.Response.ContentType = ContentTypes.GetValueOrDefault(ext, "application/octet-stream");
        await stream.CopyToAsync(context.Response.Body);
    }
}
