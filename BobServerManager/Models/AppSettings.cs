namespace BobServerManager.Models;

/// <summary>
/// Everything the tool remembers between runs. Persisted as JSON under
/// %AppData%\BobServerManager\settings.json (see Core/SettingsStore.cs).
/// </summary>
public class AppSettings
{
    public StartupSettings Startup { get; set; } = new();
    public RebootSettings Reboot { get; set; } = new();
    public NotificationSettings Notifications { get; set; } = new();
    public UpdateSettings Update { get; set; } = new();

    /// <summary>Manual override for the Game.ini path. Leave blank to auto-derive from the exe path.</summary>
    public string GameIniPathOverride { get; set; } = "";

    public bool AutoRestartOnCrash { get; set; } = true;

    /// <summary>Start the server automatically when this tool launches.</summary>
    public bool AutoStartServerOnAppLaunch { get; set; } = false;
}

/// <summary>Command line switches used to launch BeastsOfBermudaServer.exe.</summary>
public class StartupSettings
{
    public string ExePath { get; set; } = "";
    public string MapName { get; set; } = "Ancestral_Plains";
    public string GameMode { get; set; } = "Life_Cycle";
    public string SessionName { get; set; } = "My_BoB_Server";
    public string ServerPassword { get; set; } = "";
    public int NumPlayers { get; set; } = 32;
    public int Port { get; set; } = 7777;
    public int QueryPort { get; set; } = 27015;
    public string MultiHome { get; set; } = "";
    public bool UseLog { get; set; } = true;
    public string ExtraArgs { get; set; } = "";

    public static readonly string[] KnownMaps =
    {
        "Ancestral_Plains", "Forest_Island", "Rival_Shores", "Bloodbath",
        "DM_Caldera", "Test_Performance", "BB_Test_Map"
    };

    public static readonly string[] KnownGameModes = { "Life_Cycle", "Combat", "Free_Roam" };

    /// <summary>Builds the full argument string as documented on the BoB wiki / community install script:
    /// game args use "-key value", server URL args use "?key=value".</summary>
    public string BuildArguments()
    {
        var session = string.IsNullOrWhiteSpace(SessionName)
            ? "BoB_Server"
            : SessionName.Trim().Replace(' ', '_');

        var parts = new List<string>
        {
            "-GameMode", GameMode,
            "-MapName", MapName,
            "-SessionName", session,
            "-NumPlayers", NumPlayers.ToString()
        };

        if (!string.IsNullOrWhiteSpace(ServerPassword))
        {
            parts.Add("-ServerPassword");
            parts.Add(ServerPassword.Replace(" ", ""));
        }

        parts.Add($"?Port={Port}");
        parts.Add($"?QueryPort={QueryPort}");

        if (!string.IsNullOrWhiteSpace(MultiHome))
            parts.Add($"?MultiHome={MultiHome.Trim()}");

        if (UseLog)
            parts.Add("-log");

        if (!string.IsNullOrWhiteSpace(ExtraArgs))
            parts.Add(ExtraArgs.Trim());

        return string.Join(' ', parts);
    }
}

public enum RebootMode
{
    Interval,
    DailyTimes
}

/// <summary>Automatic reboot scheduling + the warning that's broadcast beforehand.</summary>
public class RebootSettings
{
    public bool Enabled { get; set; } = true;
    public RebootMode Mode { get; set; } = RebootMode.Interval;

    /// <summary>Used when Mode == Interval. Default: every 24 hours.</summary>
    public double IntervalHours { get; set; } = 24;

    /// <summary>Used when Mode == DailyTimes. Entries formatted "HH:mm" (24h, local time).</summary>
    public List<string> DailyTimes { get; set; } = new();

    /// <summary>Minutes before reboot that the main warning message goes out. Default: 5.</summary>
    public int WarningLeadMinutes { get; set; } = 5;

    /// <summary>Extra warning checkpoints (minutes-before-reboot) in addition to WarningLeadMinutes, e.g. [30, 15, 1].</summary>
    public List<int> ExtraWarningMinutes { get; set; } = new();

    /// <summary>
    /// The text broadcast to the server before reboot. Supports {minutes} and {time} placeholders.
    /// </summary>
    public string WarningMessageTemplate { get; set; } =
        "Server restarting in {minutes} minute(s) for scheduled maintenance.";

    public bool ForceSaveBeforeRestart { get; set; } = true;
    public string ShutdownCommand { get; set; } = "Exit";
    public int ShutdownTimeoutSeconds { get; set; } = 30;

    /// <summary>Bookkeeping so the schedule survives an app restart.</summary>
    public DateTime? LastRebootUtc { get; set; } = null;
}

/// <summary>Where/how the pre-reboot warning (and other admin messages) actually get delivered.</summary>
public class NotificationSettings
{
    /// <summary>Write the message to the server's own console via redirected stdin, e.g. "Announce &lt;msg&gt;".
    /// This mirrors typing directly into the dedicated server's log/console window, which is how
    /// admin commands are normally issued on a self-hosted BoB server. Recommended default.</summary>
    public bool UseConsoleAnnounce { get; set; } = true;

    public string AnnounceCommandTemplate { get; set; } = "Announce {message}";

    public bool UseDiscordWebhook { get; set; } = false;
    public string DiscordWebhookUrl { get; set; } = "";

    /// <summary>
    /// Beasts of Bermuda's built-in RCON (Game.ini [/Script/BeastsOfBermuda.RCONHandler]) is marked
    /// "Beta, not fully implemented" by the community and is documented as output-only in most setups.
    /// This is provided as a best-effort, off-by-default extra channel using the standard Source RCON
    /// wire protocol (the same one AMP's "Generic/SourceRCON" module uses for this game).
    /// </summary>
    public bool UseRcon { get; set; } = false;

    public string RconHost { get; set; } = "127.0.0.1";
    public int RconPort { get; set; } = 27015;
    public string RconPassword { get; set; } = "";
}

/// <summary>Updating the dedicated server files through SteamCMD.</summary>
public class UpdateSettings
{
    /// <summary>Steam app id of the Beasts of Bermuda Dedicated Server (per wiki.beastsofbermuda.com).</summary>
    public const string DefaultAppId = "882430";

    /// <summary>Full path to steamcmd.exe.</summary>
    public string SteamCmdPath { get; set; } = "";

    /// <summary>Server install root passed to +force_install_dir. Leave blank to derive it from the server exe path.</summary>
    public string InstallDirOverride { get; set; } = "";

    public string AppId { get; set; } = DefaultAppId;

    /// <summary>Optional Steam beta branch name (blank = public/default branch).</summary>
    public string BetaBranch { get; set; } = "";

    /// <summary>Pass "validate" to app_update (slower, but repairs missing/corrupt files).</summary>
    public bool Validate { get; set; } = true;

    /// <summary>Run the update while the server is down during every scheduled reboot.</summary>
    public bool UpdateOnScheduledReboot { get; set; } = false;

    /// <summary>Broadcast when an update is started manually while the server is running.</summary>
    public string UpdateMessage { get; set; } = "Server is restarting now to install an update.";
}
