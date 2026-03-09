using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MailPeek.Authorization;
using MailPeek.Configuration;
using MailPeek.Hubs;
using MailPeek.Middleware;

namespace MailPeek.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseMailPeek(
        this IApplicationBuilder app,
        Action<MailPeekDashboardOptions>? configureOptions = null)
    {
        var options = new MailPeekDashboardOptions();
        configureOptions?.Invoke(options);

        // Ensure path prefix doesn't have trailing slash
        options.PathPrefix = options.PathPrefix.TrimEnd('/');

        // Authorization middleware — only for dashboard routes
        if (options.Authorization.Length > 0)
        {
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? "";
                if (path.StartsWith(options.PathPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var mailPeekAuthContext = new MailPeekAuthContext(context);
                    if (!options.Authorization.All(f => f.Authorize(mailPeekAuthContext)))
                    {
                        context.Response.StatusCode = 403;
                        return;
                    }
                }

                await next();
            });
        }

        // Static files middleware (dashboard UI)
        app.UseMiddleware<DashboardStaticFilesMiddleware>(options);

        // API and SignalR endpoints
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapMailPeekApi(options.PathPrefix);
            endpoints.MapHub<MailPeekHub>($"{options.PathPrefix}/hub");
        });

        // Start the SignalR notifier
        var notifier = app.ApplicationServices.GetRequiredService<MailPeekHubNotifier>();
        notifier.Start();

        return app;
    }
}
