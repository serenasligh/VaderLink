using VaderLink.Config;

namespace VaderLink.Tray;

/// <summary>
/// Minimal settings window reachable from the tray icon context menu.
/// Kept intentionally simple for v1.
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly AppConfig _config;
    public event Action<AppConfig>? ConfigSaved;

    public SettingsForm(AppConfig config)
    {
        _config = config;

        Text            = "VaderLink Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        ClientSize      = new Size(360, 310);

        var deviceIdLabel = new Label
        {
            Text     = "vJoy Device ID:",
            Location = new Point(16, 20),
            AutoSize = true,
        };

        var deviceIdSpinner = new NumericUpDown
        {
            Minimum  = 1,
            Maximum  = 16,
            Value    = _config.VJoyDeviceId,
            Location = new Point(160, 17),
            Width    = 60,
        };

        var startMinCheck = new CheckBox
        {
            Text     = "Start minimised to tray",
            Checked  = _config.StartMinimized,
            Location = new Point(16, 60),
            AutoSize = true,
        };

        var startWithWindowsCheck = new CheckBox
        {
            Text     = "Start with Windows",
            Checked  = _config.StartWithWindows,
            Location = new Point(16, 90),
            AutoSize = true,
        };

        var notifyCheck = new CheckBox
        {
            Text     = "Show connection notifications",
            Checked  = _config.ShowConnectionNotifications,
            Location = new Point(16, 120),
            AutoSize = true,
        };

        // ── Motion / gyro section ─────────────────────────────────────────────
        var motionSeparator = new Label
        {
            Text      = "─── Motion (Gyro / Accel) ───────────────",
            Location  = new Point(16, 155),
            AutoSize  = true,
            ForeColor = SystemColors.GrayText,
        };

        var enableMotionCheck = new CheckBox
        {
            Text     = "Enable motion axes (requires vJoy Device 2)",
            Checked  = _config.EnableMotion,
            Location = new Point(16, 178),
            AutoSize = true,
        };

        var motionDeviceIdLabel = new Label
        {
            Text     = "Motion vJoy Device ID:",
            Location = new Point(16, 212),
            AutoSize = true,
        };

        var motionDeviceIdSpinner = new NumericUpDown
        {
            Minimum  = 1,
            Maximum  = 16,
            Value    = _config.MotionVJoyDeviceId,
            Location = new Point(200, 209),
            Width    = 60,
            Enabled  = _config.EnableMotion,
        };

        enableMotionCheck.CheckedChanged += (_, _) =>
            motionDeviceIdSpinner.Enabled = enableMotionCheck.Checked;

        var motionNote = new Label
        {
            Text      = "Configure vJoy Device 2 with axes X/Y/Z/Rx/Ry/Rz in 'Configure vJoy'.",
            Location  = new Point(16, 238),
            Size      = new Size(328, 32),
            ForeColor = SystemColors.GrayText,
        };

        var saveButton = new Button
        {
            Text     = "Save",
            Location = new Point(180, 276),
            Width    = 80,
        };

        var cancelButton = new Button
        {
            Text     = "Cancel",
            Location = new Point(270, 276),
            Width    = 80,
        };

        saveButton.Click += (_, _) =>
        {
            _config.VJoyDeviceId               = (uint)deviceIdSpinner.Value;
            _config.StartMinimized             = startMinCheck.Checked;
            _config.StartWithWindows           = startWithWindowsCheck.Checked;
            _config.ShowConnectionNotifications = notifyCheck.Checked;
            _config.EnableMotion               = enableMotionCheck.Checked;
            _config.MotionVJoyDeviceId         = (uint)motionDeviceIdSpinner.Value;

            ConfigManager.SetStartWithWindows(_config.StartWithWindows);
            ConfigManager.Save(_config);
            ConfigSaved?.Invoke(_config);
            Close();
        };

        cancelButton.Click += (_, _) => Close();

        Controls.AddRange([
            deviceIdLabel, deviceIdSpinner,
            startMinCheck, startWithWindowsCheck, notifyCheck,
            motionSeparator, enableMotionCheck,
            motionDeviceIdLabel, motionDeviceIdSpinner, motionNote,
            saveButton, cancelButton,
        ]);
    }
}
