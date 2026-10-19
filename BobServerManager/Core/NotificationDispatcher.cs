using BobServerManager.Models;

namespace BobServerManager.Core;

/// <summary>Fans a single message out to every notification channel the user has enabled.</summary>
public class NotificationDispatcher
{
    private readonly ServerProcessManager _server;
    public event Action<string>? Log;

    public NotificationDispatcher(ServerProcessManager server) => _server = server;

    public async Task SendAsync(NotificationSettings settings, string message)
    {
        if (settings.UseConsoleAnnounce)
        {
            var cmd = settings.AnnounceCommandTemplate.Replace("{message}", message);
            var ok = _server.SendConsoleCommand(cmd);
            Log?.Invoke(ok
                ? $"[notify] Console announce sent: {cmd}"
                : "[notify] Console announce FAILED (is the server running?).");
        }

        if (settings.UseDiscordWebhook)
        {
            var (ok, info) = await DiscordNotifier.SendAsync(settings.DiscordWebhookUrl, message);
            Log?.Invoke(ok ? "[notify] Discord webhook sent." : $"[notify] Discord webhook FAILED: {info}");
        }

        if (settings.UseRcon)
        {
            using var rcon = new SourceRconClient();
            var (authOk, authInfo) = await rcon.ConnectAndAuthAsync(settings.RconHost, settings.RconPort, settings.RconPassword);
            if (!authOk)
            {
                Log?.Invoke($"[notify] RCON FAILED (experimental channel): {authInfo}");
            }
            else
            {
                var cmd = settings.AnnounceCommandTemplate.Replace("{message}", message);
                var (ok, resp) = await rcon.ExecCommandAsync(cmd);
                Log?.Invoke(ok ? $"[notify] RCON command sent: {cmd}" : $"[notify] RCON exec FAILED: {resp}");
            }
        }
    }
}
