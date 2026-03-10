using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MailPeek.Hubs;
using MailPeek.Storage;

namespace MailPeek.Middleware;

public static class DashboardApiExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

#pragma warning disable MA0051
    public static IEndpointRouteBuilder MapMailPeekApi(this IEndpointRouteBuilder endpoints, string pathPrefix)
#pragma warning restore MA0051
    {
        var api = $"{pathPrefix}/api";

        endpoints.MapGet($"{api}/messages", (HttpContext context, IMessageStore store) =>
        {
            var page = int.TryParse(context.Request.Query["page"], CultureInfo.InvariantCulture, out var p) ? p : 0;
            var size = int.TryParse(context.Request.Query["size"], CultureInfo.InvariantCulture, out var s) ? s : 50;
            var search = context.Request.Query["search"].FirstOrDefault();
            var tag = context.Request.Query["tag"].FirstOrDefault();

            var result = store.GetPage(page, size, search, tag);
            var unreadCount = store.GetUnreadCount();
            return Results.Json(new
            {
                items = result.Items.Select(m => m.ToSummary()),
                result.TotalCount,
                unreadCount
            }, JsonOptions);
        });

        endpoints.MapGet($"{api}/messages/{{id:guid}}", (Guid id, IMessageStore store) =>
        {
            var msg = store.GetById(id);
            return msg is null ? Results.NotFound() : Results.Json(msg, JsonOptions);
        });

        endpoints.MapGet($"{api}/messages/{{id:guid}}/html", async (Guid id, HttpContext context, IMessageStore store) =>
        {
            var msg = store.GetById(id);
            if (msg is null) { context.Response.StatusCode = 404; return; }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(msg.HtmlBody ?? "<em>No HTML body</em>").ConfigureAwait(false);
        });

        endpoints.MapGet($"{api}/messages/{{id:guid}}/attachments/{{index:int}}", async (Guid id, int index, HttpContext context, IMessageStore store) =>
        {
            var msg = store.GetById(id);
            if (msg is null || index < 0 || index >= msg.Attachments.Count)
            {
                context.Response.StatusCode = 404;
                return;
            }

            var attachment = msg.Attachments[index];
            context.Response.ContentType = attachment.ContentType;
            context.Response.Headers.ContentDisposition = $"attachment; filename=\"{attachment.FileName}\"";
            await context.Response.Body.WriteAsync(attachment.Content).ConfigureAwait(false);
        });

        endpoints.MapDelete($"{api}/messages/{{id:guid}}", async (Guid id, IMessageStore store, HttpContext context) =>
        {
            var notifier = context.RequestServices.GetService<MailPeekHubNotifier>();
            if (!store.Delete(id)) return Results.NotFound();

            if (notifier is not null)
                await notifier.NotifyMessageDeleted(id).ConfigureAwait(false);

            return Results.Ok();
        });

        endpoints.MapDelete($"{api}/messages/bulk", async (HttpContext context, IMessageStore store) =>
        {
            var notifier = context.RequestServices.GetService<MailPeekHubNotifier>();
            BulkDeleteRequest? body;
            try
            {
                body = await context.Request.ReadFromJsonAsync<BulkDeleteRequest>().ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                return Results.BadRequest();
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }

            if (body?.Ids is null || body.Ids.Count == 0) return Results.BadRequest();

            var deleted = store.DeleteMany(body.Ids);

            if (notifier is not null)
                await notifier.NotifyMessagesDeleted(body.Ids).ConfigureAwait(false);

            return Results.Json(new { deleted }, JsonOptions);
        });

        endpoints.MapDelete($"{api}/messages", async (IMessageStore store, HttpContext context) =>
        {
            var notifier = context.RequestServices.GetService<MailPeekHubNotifier>();
            store.Clear();

            if (notifier is not null)
                await notifier.NotifyMessagesCleared().ConfigureAwait(false);

            return Results.Ok();
        });

        endpoints.MapPut($"{api}/messages/{{id:guid}}/read", (Guid id, IMessageStore store) =>
        {
            return store.MarkAsRead(id) ? Results.Ok() : Results.NotFound();
        });

        endpoints.MapGet($"{api}/messages/{{id:guid}}/links", (Guid id, IMessageStore store) =>
        {
            var msg = store.GetById(id);
            if (msg is null) return Results.NotFound();
            if (!msg.LinkCheckComplete)
                return Results.Json(new { status = "checking" }, JsonOptions, statusCode: 202);
            return Results.Json(msg.LinkCheckResults, JsonOptions);
        });

        endpoints.MapPut($"{api}/messages/{{id:guid}}/tags", async (Guid id, HttpContext context, IMessageStore store) =>
        {
            var tags = await context.Request.ReadFromJsonAsync<List<string>>().ConfigureAwait(false);
            if (tags is null) return Results.BadRequest();
            return store.SetTags(id, tags) ? Results.Ok() : Results.NotFound();
        });

        return endpoints;
    }

    private sealed record BulkDeleteRequest(List<Guid> Ids);
}
