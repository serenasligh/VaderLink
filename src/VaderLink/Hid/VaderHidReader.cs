using System.Threading.Channels;
using VaderLink.Model;

namespace VaderLink.Hid;

/// <summary>
/// Runs a dedicated background thread that:
///   1. Discovers and opens the Vader 5 Pro vendor HID interface (writable channel only).
///   2. Reads and parses V2 input reports in a tight loop.
///   3. Publishes parsed <see cref="ControllerState"/> values to a bounded Channel.
///   4. Sends a heartbeat every 30 s to maintain vendor interface acquisition.
///   5. Periodically queries battery status.
/// </summary>
public sealed class VaderHidReader : IDisposable
{
    // ── Events raised on the calling thread via SynchronizationContext ────────
    public event Action<string>?     OnConnected;     // arg: device info string
    public event Action<string>?     OnDisconnected;  // arg: reason
    public event Action<string>?     OnError;         // arg: user-facing message
    public event Action<byte, bool>? OnBatteryUpdate; // (percent, isCharging)

    // ── Channel shared with the output consumer (App.cs) ─────────────────────
    public ChannelReader<ControllerState> StateReader => _channel.Reader;

    // Bounded capacity 2: drop stale frames rather than accumulate latency.
    private readonly Channel<ControllerState> _channel =
        Channel.CreateBounded<ControllerState>(new BoundedChannelOptions(2)
        {
            FullMode     = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true,
        });

    private readonly SynchronizationContext? _syncCtx;
    private CancellationTokenSource?         _cts;
    private Thread?                          _thread;
    private bool                             _disposed;

    // Battery state — updated from device-info responses only.
    private byte _batteryPercent;
    private bool _isCharging;
    private bool _batteryKnown; // true once we've received a valid battery reading

    // Error debounce — suppress identical consecutive errors within the window.
    private string   _lastErrorMessage  = string.Empty;
    private DateTime _lastErrorTime     = DateTime.MinValue;
    private static readonly TimeSpan ErrorDebounceInterval = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan HeartbeatInterval   = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BatteryPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AcquireTimeout      = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay          = TimeSpan.FromSeconds(3);

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

            // ── Connect + acquire ─────────────────────────────────────────────
            // TryOpen() iterates all candidate interfaces and returns the first
            // that successfully accepts the V2 acquire command write.
            if (!device.TryOpen(out string openError))
            {
                PostErrorDebounced(openError);
                WaitOrCancel(RetryDelay, token);
                continue;
            }

            // Request device info immediately so battery shows up quickly.
            try { device.Write(V2Protocol.DeviceInfoCmd); }
            catch { /* non-fatal — battery will just update on next scheduled poll */ }

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
                        // Try input report first.
                        var state = V2Protocol.TryParseInputReport(data, _batteryPercent, _isCharging);
                        if (state.HasValue)
                        {
                            everReceivedInputReport = true;
                            _channel.Writer.TryWrite(state.Value);
                        }
                        else
                        {
                            // Try device-info / battery response.
                            if (V2Protocol.TryParseDeviceInfo(data, out _, out byte pct, out bool charging))
                            {
                                bool changed = pct != _batteryPercent || charging != _isCharging || !_batteryKnown;
                                _batteryPercent = pct;
                                _isCharging     = charging;
                                _batteryKnown   = true;
                                if (changed)
                                    Post(OnBatteryUpdate, (pct, charging));
                            }
                        }
                    }

                    // Check acquire timeout: if no input report has arrived within 3 s,
                    // the Space Station toggle is likely not enabled.
                    if (!everReceivedInputReport &&
                        DateTime.UtcNow - acquireStart > AcquireTimeout)
                    {
                        PostErrorDebounced(
                            "No data received from Vader 5 Pro enhanced mode.\n\n" +
                            "Please open Flydigi Space Station and enable:\n" +
                            "  \"Allow third-party apps to take over mappings\"\n\n" +
                            "Then reconnect the controller.");
                        break;
                    }

                    // Heartbeat: re-send acquire every 30 s to keep the channel active.
                    if (DateTime.UtcNow - lastHeartbeat > HeartbeatInterval)
                    {
                        device.Write(V2Protocol.AcquireCmd);
                        lastHeartbeat = DateTime.UtcNow;
                    }

                    // Periodic battery poll.
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
                PostErrorDebounced($"Unexpected read error: {ex.Message}");
            }
            finally
            {
                try { device.Write(V2Protocol.ReleaseCmd); } catch { /* best-effort */ }
                _batteryKnown = false; // reset so "--" shows again on next connect
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
        catch (OperationCanceledException) { /* normal shutdown */ }
    }

    /// <summary>
    /// Fires OnError, but suppresses the notification if the same message was already
    /// shown within <see cref="ErrorDebounceInterval"/>. Prevents notification floods
    /// during rapid retry loops.
    /// </summary>
    private void PostErrorDebounced(string message)
    {
        var now = DateTime.UtcNow;
        if (message == _lastErrorMessage && now - _lastErrorTime < ErrorDebounceInterval)
            return;
        _lastErrorMessage = message;
        _lastErrorTime    = now;
        Post(OnError, message);
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
