using MailPeek.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMailPeek(options =>
{
    options.Port = 2525;
    options.MaxMessages = 500;
});

var app = builder.Build();

app.UseMailPeek(options =>
{
    options.PathPrefix = "/mailpeek";
    options.Title = "MailPeek";
});

app.MapGet("/", () => "Fake SMTP running. Dashboard at /mailpeek");

app.Run();
