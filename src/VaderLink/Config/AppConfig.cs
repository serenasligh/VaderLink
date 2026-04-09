namespace VaderLink.Config;
 
/// <summary>
/// Root configuration POCO serialised to/from JSON in %APPDATA%\VaderLink\config.json.
/// All properties have sensible defaults so the file is optional on first run.
/// </summary>
public sealed class AppConfig
{
    /// <summary>vJoy device number to acquire (1-based). Default: 1.</summary>
    public uint VJoyDeviceId { get; set; } = 1;
 
    /// <summary>Start the application minimised to the system tray. Default: true.</summary>
    public bool StartMinimized { get; set; } = true;
 
    /// <summary>Register VaderLink in HKCU Run key so it launches with Windows.</summary>
    public bool StartWithWindows { get; set; } = false;
 
    /// <summary>Show a balloon tip when the controller connects/disconnects.</summary>
    public bool ShowConnectionNotifications { get; set; } = true;

    /// <summary>
    /// Enable the motion (gyro/accel) output to a second vJoy device.
    /// Requires a second vJoy device configured in "Configure vJoy" with
    /// axes X/Y/Z/Rx/Ry/Rz enabled. Default: false (opt-in).
    /// </summary>
    public bool EnableMotion { get; set; } = false;

    /// <summary>vJoy device number used for gyro/accel axes. Default: 2.</summary>
    public uint MotionVJoyDeviceId { get; set; } = 2;
}
