using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmtpServer.Dashboard.Authorization;
using SmtpServer.Dashboard.Configuration;
using SmtpServer.Dashboard.Hubs;
using SmtpServer.Dashboard.Middleware;

namespace SmtpServer.Dashboard.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseFakeSmtpDashboard(
        this IApplicationBuilder app,
        Action<FakeDashboardOptions>? configureOptions = null)
    {
        var options = new FakeDashboardOptions();
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
                    var dashboardContext = new DashboardContext(context);
                    if (!options.Authorization.All(f => f.Authorize(dashboardContext)))
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
            endpoints.MapDashboardApi(options.PathPrefix);
            endpoints.MapHub<SmtpDashboardHub>($"{options.PathPrefix}/hub");
        });

        // Start the SignalR notifier
        var notifier = app.ApplicationServices.GetRequiredService<SmtpDashboardHubNotifier>();
        notifier.Start();

        return app;
    }
}
