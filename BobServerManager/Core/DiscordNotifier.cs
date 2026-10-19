using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace BobServerManager.Core;

/// <summary>Posts plain messages to a Discord channel webhook - useful as a reliable, guaranteed-to-work
/// notification channel alongside (or instead of) in-game console announcements.</summary>
public static class DiscordNotifier
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<(bool ok, string info)> SendAsync(string webhookUrl, string message)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            return (false, "No webhook URL configured.");

        try
        {
            var payload = JsonSerializer.Serialize(new { content = message });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await Http.PostAsync(webhookUrl, content);
            if (resp.IsSuccessStatusCode) return (true, "OK");
            return (false, $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
