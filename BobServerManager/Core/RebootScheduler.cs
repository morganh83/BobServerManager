using BobServerManager.Models;

namespace BobServerManager.Core;

/// <summary>
/// Drives the automatic reboot cycle: figures out when the next reboot is due, fires the warning
/// message(s) at the configured lead time(s), and performs the actual stop/relaunch. Call
/// <see cref="TickAsync"/> periodically (e.g. every 5-10s) from a UI timer.
/// </summary>
public class RebootScheduler
{
    private readonly AppSettings _settings;
    private readonly ServerProcessManager _server;
    private readonly NotificationDispatcher _dispatcher;
    private readonly Func<(string exePath, string args)> _buildLaunch;
    private readonly HashSet<int> _warnedThisCycle = new();
    private bool _rebooting;

    public DateTime? NextRebootUtc { get; private set; }
    public event Action<string>? Log;

    public RebootScheduler(AppSettings settings, ServerProcessManager server,
        NotificationDispatcher dispatcher, Func<(string exePath, string args)> buildLaunch)
    {
        _settings = settings;
        _server = server;
        _dispatcher = dispatcher;
        _buildLaunch = buildLaunch;
    }

    public void RecomputeNext()
    {
        var r = _settings.Reboot;
        var now = DateTime.UtcNow;

        if (r.Mode == RebootMode.DailyTimes && r.DailyTimes.Count > 0)
        {
            DateTime? best = null;
            var nowLocal = DateTime.Now;
            foreach (var entry in r.DailyTimes)
            {
                if (!TimeSpan.TryParse(entry, out var tod)) continue;
                for (int dayOffset = 0; dayOffset <= 1; dayOffset++)
                {
                    var candidateLocal = nowLocal.Date.AddDays(dayOffset).Add(tod);
                    if (candidateLocal <= nowLocal) continue;
                    if (best is null || candidateLocal < best) best = candidateLocal;
                    break;
                }
            }
            NextRebootUtc = (best ?? nowLocal.AddDays(1)).ToUniversalTime();
        }
        else
        {
            var lastUtc = r.LastRebootUtc ?? now;
            var candidate = lastUtc.AddHours(Math.Max(0.1, r.IntervalHours));
            if (candidate <= now)
            {
                // App was closed/offline past the scheduled time - don't slam the server with an
                // unannounced instant restart, give at least one full warning window.
                candidate = now.AddMinutes(Math.Max(1, r.WarningLeadMinutes));
            }
            NextRebootUtc = candidate;
        }

        _warnedThisCycle.Clear();
        Log?.Invoke($"[scheduler] Next reboot: {NextRebootUtc!.Value.ToLocalTime():f}");
    }

    public async Task TickAsync()
    {
        if (!_settings.Reboot.Enabled || _rebooting) return;
        if (NextRebootUtc is null) RecomputeNext();

        var remaining = NextRebootUtc!.Value - DateTime.UtcNow;
        var r = _settings.Reboot;

        var thresholds = r.ExtraWarningMinutes
            .Append(r.WarningLeadMinutes)
            .Where(m => m > 0)
            .Distinct()
            .OrderByDescending(m => m);

        foreach (var minutes in thresholds)
        {
            if (remaining <= TimeSpan.FromMinutes(minutes) && !_warnedThisCycle.Contains(minutes))
            {
                _warnedThisCycle.Add(minutes);
                var message = BuildMessage(minutes);
                Log?.Invoke($"[scheduler] Sending {minutes}-minute warning: {message}");
                await _dispatcher.SendAsync(_settings.Notifications, message);
            }
        }

        if (remaining <= TimeSpan.Zero)
            await PerformRebootAsync(update: _settings.Update.UpdateOnScheduledReboot);
    }

    private string BuildMessage(int minutes)
    {
        return _settings.Reboot.WarningMessageTemplate
            .Replace("{minutes}", minutes.ToString())
            .Replace("{time}", NextRebootUtc!.Value.ToLocalTime().ToString("t"));
    }

    /// <summary>True while a reboot or update cycle is in progress.</summary>
    public bool IsBusy => _rebooting;

    /// <summary>Runs the full stop -> relaunch cycle immediately, bypassing the schedule (used both
    /// by the automatic scheduler and the manual "Restart Now" button). When <paramref name="update"/>
    /// is true, SteamCMD updates the server files while it is down.</summary>
    public async Task PerformRebootAsync(bool update = false)
    {
        if (_rebooting) return;
        _rebooting = true;
        try
        {
            Log?.Invoke(update ? "[scheduler] Executing reboot with update..." : "[scheduler] Executing reboot...");

            await StopForMaintenanceAsync();

            _settings.Reboot.LastRebootUtc = DateTime.UtcNow;
            SettingsStore.Save(_settings);

            if (update)
                await SteamCmdUpdater.RunUpdateAsync(_settings, line => Log?.Invoke(line));

            StartServer();
            RecomputeNext();
        }
        finally
        {
            _rebooting = false;
        }
    }

    /// <summary>Manual "Update Server": announces, stops the server if it's running, runs SteamCMD,
    /// and starts the server again only if it was running beforehand.</summary>
    public async Task PerformUpdateAsync()
    {
        if (_rebooting) return;
        _rebooting = true;
        try
        {
            var wasRunning = _server.IsRunning;
            if (wasRunning)
            {
                if (!string.IsNullOrWhiteSpace(_settings.Update.UpdateMessage))
                {
                    await _dispatcher.SendAsync(_settings.Notifications, _settings.Update.UpdateMessage);
                    await Task.Delay(3000);
                }
                await StopForMaintenanceAsync();
            }

            await SteamCmdUpdater.RunUpdateAsync(_settings, line => Log?.Invoke(line));

            if (wasRunning) StartServer();
        }
        finally
        {
            _rebooting = false;
        }
    }

    private async Task StopForMaintenanceAsync()
    {
        if (!_server.IsRunning) return;

        if (_settings.Reboot.ForceSaveBeforeRestart)
        {
            _server.SendConsoleCommand("ForceServerSave");
            await Task.Delay(2000);
        }

        if (!await _server.StopGracefullyAsync(_settings.Reboot.ShutdownCommand, _settings.Reboot.ShutdownTimeoutSeconds))
            await Task.Delay(2000); // give a force-killed process time to release its file handles
    }

    private void StartServer()
    {
        try
        {
            var (exePath, args) = _buildLaunch();
            _server.Start(exePath, args);
        }
        catch (Exception ex)
        {
            Log?.Invoke("[scheduler] Failed to start server: " + ex.Message);
        }
    }
}
