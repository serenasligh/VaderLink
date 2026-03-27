using System.Threading.Channels;
using VaderLink.Model;
 
namespace VaderLink.Hid;
 
/// <summary>
/// Runs a dedicated background thread that:
///   1. Discovers and opens the Vader 5 Pro vendor HID interface.
///   2. Sends the V2 acquire command.
///   3. Reads and parses V2 input reports in a tight loop.
///   4. Publishes parsed <see cref="ControllerState"/> values to a bounded Channel.
///   5. Sends a heartbeat every 30 s to maintain the vendor interface acquisition.
/// </summary>
public sealed class VaderHidReader : IDisposable
{
    // ── Events raised on the calling thread via SynchronizationContext ────────
    public event Action<string>?       OnConnected;    // arg: device info string
    public event Action<string>?       OnDisconnected; // arg: reason
    public event Action<string>?       OnError;        // arg: user-facing message
    public event Action<byte, bool>?   OnBatteryUpdate; // (percent, isCharging)
 
    // ── Channel shared with the output consumer (App.cs) ─────────────────────
    public ChannelReader<ControllerState> StateReader => _channel.Reader;
 
    // Bounded capacity 2: drop stale frames rather than accumulate latency.
    private readonly Channel<ControllerState> _channel =
        Channel.CreateBounded<ControllerState>(new BoundedChannelOptions(2)
        {
            FullMode       = BoundedChannelFullMode.DropOldest,
            SingleWriter   = true,
            SingleReader   = true,
        });
 
    private readonly SynchronizationContext? _syncCtx;
    private CancellationTokenSource?         _cts;
    private Thread?                          _thread;
    private bool                             _disposed;
 
    // Battery state carried between reports
    private byte _batteryPercent;
    private bool _isCharging;
 
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BatteryPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AcquireTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);
 
    public VaderHidReader()
    {
        _syncCtx = SynchronizationContext.Current;
    }
 
    /// <summary>Starts the background reader thread.</summary>
    public void Start()
    {
        if (_thread is not null) return;
        _cts    = new CancellationTokenSource();
        _thread = new Thread(ReaderLoop)
        {
            IsBackground = true,
            Name         = "VaderHidReader",
        };
        _thread.Start();
    }
 
    /// <summary>Signals the reader thread to stop and waits for it to exit.</summary>
    public void Stop()
    {
        _cts?.Cancel();
        _thread?.Join(TimeSpan.FromSeconds(5));
        _thread = null;
    }
 
    // ── Background thread ─────────────────────────────────────────────────────
 
    private void ReaderLoop()
    {
        var token = _cts!.Token;
 
        while (!token.IsCancellationRequested)
        {
            using var device = new VaderHidDevice();
 
            // ── Connect ───────────────────────────────────────────────────────
            if (!device.TryOpen(out string openError))
            {
                Post(OnError, openError);
                WaitOrCancel(RetryDelay, token);
                continue;
            }
 
            // ── Acquire vendor interface ──────────────────────────────────────
            try { device.Write(V2Protocol.AcquireCmd); }
            catch (Exception ex)
            {
                Post(OnError, $"Failed to send acquire command: {ex.Message}");
                WaitOrCancel(RetryDelay, token);
                continue;
            }
 
            Post(OnConnected, "Vader 5 Pro vendor HID acquired");
 
            // ── Read loop ─────────────────────────────────────────────────────
            var lastHeartbeat   = DateTime.UtcNow;
            var lastBatteryPoll = DateTime.UtcNow;
            var acquireStart    = DateTime.UtcNow;
            bool everReceivedInputReport = false;
 
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int n = device.Read(out var data);
 
                    if (n > 0)
                    {
                        // Try parsing as input report
                        var state = V2Protocol.TryParseInputReport(data, _batteryPercent, _isCharging);
                        if (state.HasValue)
                        {
                            everReceivedInputReport = true;
                            _channel.Writer.TryWrite(state.Value);
                        }
                        else
                        {
                            // Try parsing as device-info / battery response
                            if (V2Protocol.TryParseDeviceInfo(data, out _, out byte pct, out bool charging))
                            {
                                if (pct != _batteryPercent || charging != _isCharging)
                                {
                                    _batteryPercent = pct;
                                    _isCharging     = charging;
                                    Post(OnBatteryUpdate, (pct, charging));
                                }
                            }
                        }
                    }
 
                    // Check acquire timeout: if no input report has arrived within 3 s,
                    // the Space Station toggle is likely not enabled.
                    if (!everReceivedInputReport &&
                        DateTime.UtcNow - acquireStart > AcquireTimeout)
                    {
                        Post(OnError,
                            "No data received from Vader 5 Pro enhanced mode.\n\n" +
                            "Please open Flydigi Space Station and enable:\n" +
                            "  \"Allow third-party apps to take over mappings\"\n\n" +
                            "Then reconnect the controller.");
                        break; // retry loop
                    }
 
                    // Heartbeat
                    if (DateTime.UtcNow - lastHeartbeat > HeartbeatInterval)
                    {
                        device.Write(V2Protocol.AcquireCmd);
                        lastHeartbeat = DateTime.UtcNow;
                    }
 
                    // Periodic battery poll
                    if (DateTime.UtcNow - lastBatteryPoll > BatteryPollInterval)
                    {
                        device.Write(V2Protocol.DeviceInfoCmd);
                        lastBatteryPoll = DateTime.UtcNow;
                    }
                }
            }
            catch (IOException ioEx)
            {
                Post(OnDisconnected, $"Controller disconnected: {ioEx.Message}");
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                Post(OnError, $"Unexpected read error: {ex.Message}");
            }
            finally
            {
                try { device.Write(V2Protocol.ReleaseCmd); } catch { /* best-effort */ }
                Post(OnDisconnected, "Disconnected");
            }
 
            if (!token.IsCancellationRequested)
                WaitOrCancel(RetryDelay, token);
        }
    }
 
    // ── Helpers ───────────────────────────────────────────────────────────────
 
    private static void WaitOrCancel(TimeSpan delay, CancellationToken token)
    {
        try { Task.Delay(delay, token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { /* normal */ }
    }
 
    private void Post(Action<string>? handler, string arg)
    {
        if (handler is null) return;
        if (_syncCtx is not null)
            _syncCtx.Post(_ => handler(arg), null);
        else
            handler(arg);
    }
 
    private void Post(Action<byte, bool>? handler, (byte pct, bool charging) args)
    {
        if (handler is null) return;
        if (_syncCtx is not null)
            _syncCtx.Post(_ => handler(args.pct, args.charging), null);
        else
            handler(args.pct, args.charging);
    }
 
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _channel.Writer.TryComplete();
    }
}