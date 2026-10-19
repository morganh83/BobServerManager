using System.Diagnostics;

namespace BobServerManager.Core;

/// <summary>
/// Launches and supervises the BeastsOfBermudaServer.exe child process. Stdin/stdout/stderr are
/// redirected so this tool can (a) show a live log and (b) push console commands - e.g. the
/// pre-reboot "Announce ..." warning and the graceful "Exit" shutdown - the same way an admin
/// would type them into the server's own console window.
/// </summary>
public class ServerProcessManager : IDisposable
{
    private Process? _proc;
    private readonly object _lock = new();

    public event Action<string>? OutputReceived;
    public event Action<int>? ProcessExited;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                try { return _proc is { HasExited: false }; }
                catch { return false; }
            }
        }
    }

    public int? ProcessId
    {
        get { lock (_lock) { try { return _proc?.Id; } catch { return null; } } }
    }

    public DateTime? StartedAtUtc { get; private set; }

    public void Start(string exePath, string arguments)
    {
        if (IsRunning) throw new InvalidOperationException("Server is already running.");
        if (!File.Exists(exePath)) throw new FileNotFoundException("Server executable not found.", exePath);

        var psi = new ProcessStartInfo(exePath, arguments)
        {
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };

        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) OutputReceived?.Invoke(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) OutputReceived?.Invoke("[stderr] " + e.Data); };
        proc.Exited += (_, _) =>
        {
            int code = -1;
            try { code = proc.ExitCode; } catch { /* ignore */ }
            ProcessExited?.Invoke(code);
        };

        lock (_lock) { _proc = proc; }

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        StartedAtUtc = DateTime.UtcNow;

        OutputReceived?.Invoke($"[manager] Started PID {proc.Id}: \"{exePath}\" {arguments}");
    }

    /// <summary>Best-effort: writes a line to the server's redirected stdin, equivalent to typing
    /// it into the console window and pressing Enter. Returns false (never throws) on failure.</summary>
    public bool SendConsoleCommand(string command)
    {
        Process? p;
        lock (_lock) { p = _proc; }
        if (p is null) return false;

        try
        {
            if (p.HasExited) return false;
            p.StandardInput.WriteLine(command);
            p.StandardInput.Flush();
            OutputReceived?.Invoke("[manager] > " + command);
            return true;
        }
        catch (Exception ex)
        {
            OutputReceived?.Invoke("[manager] Failed to send console command: " + ex.Message);
            return false;
        }
    }

    /// <summary>Sends the shutdown command and waits for the process to exit on its own; kills the
    /// whole process tree if it doesn't within the timeout.</summary>
    public async Task<bool> StopGracefullyAsync(string shutdownCommand, int timeoutSeconds)
    {
        Process? p;
        lock (_lock) { p = _proc; }
        if (p is null || p.HasExited) return true;

        SendConsoleCommand(shutdownCommand);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));
            await p.WaitForExitAsync(cts.Token);
            OutputReceived?.Invoke("[manager] Server exited cleanly.");
            return true;
        }
        catch (OperationCanceledException)
        {
            OutputReceived?.Invoke("[manager] Server did not exit in time - forcing termination.");
            KillTree();
            return false;
        }
    }

    public void KillTree()
    {
        Process? p;
        lock (_lock) { p = _proc; }
        if (p is null) return;
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
        catch (Exception ex) { OutputReceived?.Invoke("[manager] Kill failed: " + ex.Message); }
    }

    public void Dispose()
    {
        try { _proc?.Dispose(); } catch { /* ignore */ }
    }
}
