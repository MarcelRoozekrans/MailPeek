using SmtpServer.Dashboard.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFakeSmtp(options =>
{
    options.Port = 2525;
    options.MaxMessages = 500;
});

var app = builder.Build();

app.UseFakeSmtpDashboard(options =>
{
    options.PathPrefix = "/smtp";
    options.Title = "Dev Mail Dashboard";
});

app.MapGet("/", () => "Fake SMTP running. Dashboard at /smtp");

app.Run();
