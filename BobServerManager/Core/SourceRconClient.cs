using System.Net.Sockets;
using System.Text;

namespace BobServerManager.Core;

/// <summary>
/// Minimal client for the Valve "Source RCON" wire protocol (a simple length-prefixed TCP packet
/// format). Beasts of Bermuda's Game.ini exposes an "[/Script/BeastsOfBermuda.RCONHandler]" section
/// (CommunicationPort / IP4Binding), and third-party panels (e.g. CubeCoders AMP) talk to it using a
/// generic Source-RCON client type. The developers' own community config tool labels this feature
/// "Beta, not fully implemented yet" and only advertises it as one-way output to RCON clients, so
/// treat this as an experimental best-effort channel, not a guaranteed way to reach players -
/// the console-announce and Discord-webhook channels are the reliable ones.
/// </summary>
public class SourceRconClient : IDisposable
{
    private const int TypeExecCommand = 2;
    private const int TypeAuth = 3;

    private TcpClient? _client;
    private NetworkStream? _stream;
    private int _nextId = 1;

    public async Task<(bool ok, string info)> ConnectAndAuthAsync(string host, int port, string password, int timeoutMs = 4000)
    {
        try
        {
            _client = new TcpClient();
            var connectTask = _client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) != connectTask)
                return (false, "Connection timed out.");
            await connectTask; // propagate any connect exception

            _stream = _client.GetStream();

            var authId = _nextId++;
            await SendPacketAsync(TypeAuth, authId, password ?? "");

            // Servers may send an empty SERVERDATA_RESPONSE_VALUE before the auth response; read until
            // we see the auth response packet (id echoes back, or -1 on failed auth).
            for (int i = 0; i < 4; i++)
            {
                var (id, _, _) = await ReadPacketAsync(timeoutMs);
                if (id == -1) return (false, "Authentication rejected by server (bad/absent RCON password).");
                if (id == authId) return (true, "Authenticated.");
            }
            return (false, "No auth response received.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool ok, string response)> ExecCommandAsync(string command, int timeoutMs = 4000)
    {
        if (_stream is null) return (false, "Not connected.");
        try
        {
            var id = _nextId++;
            await SendPacketAsync(TypeExecCommand, id, command);
            var (_, _, body) = await ReadPacketAsync(timeoutMs);
            return (true, body);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task SendPacketAsync(int type, int id, string body)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        // payload = id(4) + type(4) + body + \0 + \0
        int payloadLen = 4 + 4 + bodyBytes.Length + 1 + 1;

        var buffer = new byte[4 + payloadLen];
        BitConverter.GetBytes(payloadLen).CopyTo(buffer, 0);
        BitConverter.GetBytes(id).CopyTo(buffer, 4);
        BitConverter.GetBytes(type).CopyTo(buffer, 8);
        bodyBytes.CopyTo(buffer, 12);
        // trailing two null bytes already zero-initialized

        await _stream!.WriteAsync(buffer, 0, buffer.Length);
    }

    private async Task<(int id, int type, string body)> ReadPacketAsync(int timeoutMs)
    {
        var sizeBuf = await ReadExactAsync(4, timeoutMs);
        int size = BitConverter.ToInt32(sizeBuf, 0);
        if (size < 8 || size > 1 << 20) throw new IOException("Invalid RCON packet size.");

        var payload = await ReadExactAsync(size, timeoutMs);
        int id = BitConverter.ToInt32(payload, 0);
        int type = BitConverter.ToInt32(payload, 4);
        int bodyLen = Math.Max(0, size - 8 - 2);
        string body = bodyLen > 0 ? Encoding.UTF8.GetString(payload, 8, bodyLen) : "";
        return (id, type, body);
    }

    private async Task<byte[]> ReadExactAsync(int count, int timeoutMs)
    {
        var buf = new byte[count];
        int offset = 0;
        using var cts = new CancellationTokenSource(timeoutMs);
        while (offset < count)
        {
            int read = await _stream!.ReadAsync(buf.AsMemory(offset, count - offset), cts.Token);
            if (read == 0) throw new IOException("Connection closed by remote host.");
            offset += read;
        }
        return buf;
    }

    public void Dispose()
    {
        try { _stream?.Dispose(); } catch { /* ignore */ }
        try { _client?.Dispose(); } catch { /* ignore */ }
    }
}
