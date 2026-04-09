using VaderLink.Config;
 
namespace VaderLink.Tray;
 
/// <summary>
/// Manages the system tray icon, context menu, and balloon tip notifications.
/// Must be created and interacted with on the WinForms UI thread.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon       _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _batteryItem;
    private readonly ToolStripMenuItem _startWithWindowsItem;
 
    private AppConfig _config;
    private bool      _disposed;
 
    public event Action? ExitRequested;
    public event Action<AppConfig>? ConfigChanged;
 
    public TrayIcon(AppConfig config)
    {
        _config = config;
 
        _statusItem  = new ToolStripMenuItem("Status: Starting…") { Enabled = false };
        _batteryItem = new ToolStripMenuItem("Battery: --")       { Enabled = false };
 
        _startWithWindowsItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = config.StartWithWindows,
        };
        _startWithWindowsItem.Click += OnStartWithWindowsToggle;
 
        var showNotificationsItem = new ToolStripMenuItem("Connection notifications")
        {
            Checked = config.ShowConnectionNotifications,
        };
        showNotificationsItem.Click += (_, _) =>
        {
            _config.ShowConnectionNotifications = !_config.ShowConnectionNotifications;
            showNotificationsItem.Checked = _config.ShowConnectionNotifications;
            ConfigChanged?.Invoke(_config);
        };
 
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
 
        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_batteryItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startWithWindowsItem);
        menu.Items.Add(showNotificationsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
 
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Text             = "VaderLink - Starting…",
            Visible          = true,
            Icon             = LoadIcon("icon_disconnected.ico"),
        };
    }
 
    // ── State update methods (call from UI thread) ────────────────────────────
 
    public void SetConnected(string deviceInfo)
    {
        _notifyIcon.Icon = LoadIcon("icon_connected.ico");
        _notifyIcon.Text = "VaderLink - Connected";
        _statusItem.Text = $"Status: Connected";
 
        if (_config.ShowConnectionNotifications)
        {
            _notifyIcon.ShowBalloonTip(
                timeout: 2000,
                tipTitle: "VaderLink",
                tipText:  "Vader 5 Pro connected",
                tipIcon:  ToolTipIcon.Info);
        }
    }
 
    public void SetDisconnected(string reason)
    {
        _notifyIcon.Icon = LoadIcon("icon_disconnected.ico");
        _notifyIcon.Text = "VaderLink - Disconnected";
        _statusItem.Text = "Status: Disconnected";
        _batteryItem.Text = "Battery: --";
 
        if (_config.ShowConnectionNotifications)
        {
            _notifyIcon.ShowBalloonTip(
                timeout: 2000,
                tipTitle: "VaderLink",
                tipText:  "Vader 5 Pro disconnected",
                tipIcon:  ToolTipIcon.Warning);
        }
    }
 
    public void SetError(string message)
    {
        _notifyIcon.Icon = LoadIcon("icon_error.ico");
        _notifyIcon.Text = "VaderLink - Error";
        _statusItem.Text = "Status: Error";
 
        _notifyIcon.ShowBalloonTip(
            timeout: 6000,
            tipTitle: "VaderLink - Error",
            tipText:  message,
            tipIcon:  ToolTipIcon.Error);
    }
 
    public void SetBattery(byte percent, bool isCharging)
    {
        string charge = isCharging ? " (charging)" : "";
        _batteryItem.Text = $"Battery: {percent}%{charge}";
    }
 
    // ── Helpers ───────────────────────────────────────────────────────────────
 
    private void OnStartWithWindowsToggle(object? sender, EventArgs e)
    {
        _config.StartWithWindows = !_config.StartWithWindows;
        _startWithWindowsItem.Checked = _config.StartWithWindows;
        ConfigManager.SetStartWithWindows(_config.StartWithWindows);
        ConfigChanged?.Invoke(_config);
    }
 
    private static Icon LoadIcon(string filename)
    {
        var dir  = AppContext.BaseDirectory;
        var path = Path.Combine(dir, "Resources", filename);
 
        if (File.Exists(path))
            return new Icon(path);
 
        // Fallback: use the application's own icon (avoids crash if resources are missing)
        return SystemIcons.Application;
    }
 
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
