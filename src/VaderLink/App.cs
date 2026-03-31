using System.Threading.Channels;
using VaderLink.Config;
using VaderLink.Hid;
using VaderLink.Mapping;
using VaderLink.Model;
using VaderLink.Output;
using VaderLink.Tray;

namespace VaderLink;

/// <summary>
/// Top-level application controller. Wires together all layers and owns the
/// output consumer loop (Thread 3).
///
/// Threading overview:
///   UI thread      — WinForms message pump, TrayIcon, settings
///   HID reader     — VaderHidReader (internal thread)
///   Output thread  — this class, ConsumerLoop()
/// </summary>
public sealed class App : IDisposable
{
    private readonly AppConfig       _config;
    private readonly TrayIcon        _trayIcon;
    private readonly VaderHidReader  _hidReader;
    private readonly IVirtualController _virtualController;

    private Thread?                  _outputThread;
    private CancellationTokenSource? _cts;
    private bool                     _disposed;

    public App(AppConfig config)
    {
        _config       = config;
        _trayIcon     = new TrayIcon(config);
        _hidReader    = new VaderHidReader();
        _virtualController = new VJoyController(config.VJoyDeviceId);

        // Wire HID reader events → tray icon (all callbacks arrive on UI thread
        // because VaderHidReader captures SynchronizationContext on construction).
        _hidReader.OnConnected    += _trayIcon.SetConnected;
        _hidReader.OnDisconnected += _trayIcon.SetDisconnected;
        _hidReader.OnError        += _trayIcon.SetError;
        _hidReader.OnBatteryUpdate += _trayIcon.SetBattery;

        _trayIcon.ExitRequested  += OnExitRequested;
        _trayIcon.ConfigChanged  += OnConfigChanged;

        _virtualController.ErrorOccurred += msg =>
            _trayIcon.SetError($"vJoy error: {msg}");
    }

    public void Run()
    {
        // Connect virtual output device
        if (!_virtualController.Connect(out string vJoyError))
        {
            _trayIcon.SetError(
                $"Could not connect vJoy Device {_config.VJoyDeviceId}:\n\n{vJoyError}\n\n" +
                "See the README for vJoy installation instructions.");
            // Don't abort — carry on so tray shows the error and user can fix and restart
        }

        // Start background workers
        _hidReader.Start();

        // Start output consumer thread
        _cts          = new CancellationTokenSource();
        _outputThread = new Thread(() => ConsumerLoop(_cts.Token))
        {
            IsBackground = true,
            Name         = "VaderOutput",
        };
        _outputThread.Start();
    }

    // ── Output consumer loop (Thread 3) ──────────────────────────────────────

    private void ConsumerLoop(CancellationToken token)
    {
        var reader = _hidReader.StateReader;

        while (!token.IsCancellationRequested)
        {
            ControllerState state;
            try
            {
                // Block until a new state is available or cancellation fires.
                var valueTask = reader.ReadAsync(token);
                if (!valueTask.IsCompleted)
                    state = valueTask.AsTask().GetAwaiter().GetResult();
                else
                    state = valueTask.Result;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ChannelClosedException)
            {
                break;
            }

            var report = Mapper.Map(in state);

            _virtualController.Submit(in report);
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private static void OnExitRequested()
    {
        Application.Exit();
    }

    private void OnConfigChanged(AppConfig updated)
    {
        ConfigManager.Save(updated);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _outputThread?.Join(TimeSpan.FromSeconds(3));

        _hidReader.Dispose();
        _virtualController.Dispose();
        _trayIcon.Dispose();
    }
}
