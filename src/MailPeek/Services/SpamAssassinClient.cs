using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using MailPeek.Configuration;
using MailPeek.Models;
using Microsoft.Extensions.Logging;

namespace MailPeek.Services;

public static partial class SpamAssassinClient
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);

    public static async Task<SpamCheckResult?> CheckAsync(
        byte[] rawMessage,
        SpamAssassinOptions options,
        ILogger logger)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(options.Host, options.Port);
            if (await Task.WhenAny(connectTask, Task.Delay(ConnectionTimeout)).ConfigureAwait(false) != connectTask)
            {
                logger.LogWarning("SpamAssassin connection timed out ({Host}:{Port})", options.Host, options.Port);
                return null;
            }

            await connectTask.ConfigureAwait(false);

            var stream = client.GetStream();
            await using (stream.ConfigureAwait(false))
            {
                var command = BuildCheckCommand(rawMessage);
                var commandBytes = Encoding.UTF8.GetBytes(command);

                await stream.WriteAsync(commandBytes).ConfigureAwait(false);
                await stream.WriteAsync(rawMessage).ConfigureAwait(false);
                client.Client.Shutdown(SocketShutdown.Send);

                using var reader = new StreamReader(stream, Encoding.UTF8);
                var response = await reader.ReadToEndAsync().ConfigureAwait(false);
                return ParseCheckResponse(response);
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "SpamAssassin check failed ({Host}:{Port})", options.Host, options.Port);
            return null;
        }
    }

    public static string BuildCheckCommand(byte[] rawMessage)
    {
        var sb = new StringBuilder();
        sb.Append("CHECK SPAMC/1.5\r\n");
        sb.Append(CultureInfo.InvariantCulture, $"Content-length: {rawMessage.Length}\r\n");
        sb.Append("\r\n");
        return sb.ToString();
    }

    public static SpamCheckResult? ParseCheckResponse(string response)
    {
        var match = SpamScoreRegex().Match(response);
        if (!match.Success) return null;

        if (!double.TryParse(match.Groups["score"].Value, CultureInfo.InvariantCulture, out var score))
            return null;

        return new SpamCheckResult
        {
            Score = score,
            Source = "spamassassin"
        };
    }

    [GeneratedRegex(@"Spam:\s*(?:True|False)\s*;\s*(?<score>[\d.]+)\s*/\s*[\d.]+", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SpamScoreRegex();
}
