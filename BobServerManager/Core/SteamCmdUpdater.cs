using System.Diagnostics;
using System.IO.Compression;
using BobServerManager.Models;

namespace BobServerManager.Core;

/// <summary>
/// Installs/updates the dedicated server files with SteamCMD
/// ("+force_install_dir &lt;dir&gt; +login anonymous +app_update 882430 validate +quit").
/// </summary>
public static class SteamCmdUpdater
{
    private const string SteamCmdZipUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";

    public static readonly string DefaultSteamCmdDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BobServerManager", "steamcmd");

    /// <summary>The install root SteamCMD should write into. The server exe normally lives at
    /// &lt;root&gt;\BeastsOfBermuda\Binaries\Win64\BeastsOfBermudaServer.exe, or directly in the root.</summary>
    public static string? ResolveInstallDir(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Update.InstallDirOverride))
            return settings.Update.InstallDirOverride.Trim();

        var exe = settings.Startup.ExePath;
        if (string.IsNullOrWhiteSpace(exe)) return null;

        var dir = new DirectoryInfo(Path.GetDirectoryName(exe) ?? "");
        if (dir.Name.Equals("Win64", StringComparison.OrdinalIgnoreCase) &&
            dir.Parent?.Name.Equals("Binaries", StringComparison.OrdinalIgnoreCase) == true &&
            dir.Parent.Parent?.Parent is { } root)
            return root.FullName;

        return dir.FullName;
    }

    public static string BuildArguments(UpdateSettings u, string installDir)
    {
        var appId = string.IsNullOrWhiteSpace(u.AppId) ? UpdateSettings.DefaultAppId : u.AppId.Trim();
        var args = $"+force_install_dir \"{installDir.TrimEnd('\\')}\" +login anonymous +app_update {appId}";
        if (!string.IsNullOrWhiteSpace(u.BetaBranch)) args += $" -beta {u.BetaBranch.Trim()}";
        if (u.Validate) args += " validate";
        return args + " +quit";
    }

    /// <summary>Runs SteamCMD to completion. The server must already be stopped. Returns true on success.</summary>
    public static async Task<bool> RunUpdateAsync(AppSettings settings, Action<string> log)
    {
        var steamCmd = settings.Update.SteamCmdPath;
        if (string.IsNullOrWhiteSpace(steamCmd) || !File.Exists(steamCmd))
        {
            log("[update] steamcmd.exe not found - set its path on the Server Updates tab.");
            return false;
        }

        var installDir = ResolveInstallDir(settings);
        if (installDir is null)
        {
            log("[update] No install directory - set the server executable path or an install directory override.");
            return false;
        }

        var args = BuildArguments(settings.Update, installDir);
        log($"[update] Running: \"{steamCmd}\" {args}");

        var psi = new ProcessStartInfo(steamCmd, args)
        {
            WorkingDirectory = Path.GetDirectoryName(steamCmd) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        bool sawSuccess = false;
        try
        {
            using var proc = new Process { StartInfo = psi };
            proc.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                if (e.Data.Contains("Success! App", StringComparison.OrdinalIgnoreCase) ||
                    e.Data.Contains("already up to date", StringComparison.OrdinalIgnoreCase))
                    sawSuccess = true;
                log("[steamcmd] " + e.Data);
            };
            proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log("[steamcmd] " + e.Data); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();

            var ok = proc.ExitCode == 0 || sawSuccess;
            log(ok ? "[update] Server files are up to date." : $"[update] SteamCMD FAILED (exit code {proc.ExitCode}).");
            return ok;
        }
        catch (Exception ex)
        {
            log("[update] Failed to run SteamCMD: " + ex.Message);
            return false;
        }
    }

    /// <summary>Downloads and unpacks the official SteamCMD zip; returns the path to steamcmd.exe.</summary>
    public static async Task<string> DownloadSteamCmdAsync(string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        var zipPath = Path.Combine(targetDir, "steamcmd.zip");

        using (var http = new HttpClient())
        await using (var file = File.Create(zipPath))
        await using (var stream = await http.GetStreamAsync(SteamCmdZipUrl))
            await stream.CopyToAsync(file);

        ZipFile.ExtractToDirectory(zipPath, targetDir, overwriteFiles: true);
        File.Delete(zipPath);
        return Path.Combine(targetDir, "steamcmd.exe");
    }
}
