using System.Runtime.InteropServices;
 
namespace VaderLink.Output;
 
/// <summary>
/// Virtual controller output via the vJoy driver.
///
/// Prerequisites (user must install before running VaderLink):
///   1. vJoy driver — https://github.com/jshafer817/vJoy/releases (or BrunnerInnovation fork)
///   2. Configure vJoy Device 1 in "Configure vJoy" with:
///        • Axes enabled: X, Y, Z, Rx, Ry, Rz
///        • Number of buttons: 32 (or at least 23)
///        • POV Hat Switches: 1 (Continuous)
///   3. Place vJoyInterface.dll (64-bit) in the same folder as VaderLink.exe
///      (typically found at C:\Program Files\vJoy\x64\vJoyInterface.dll)
///
/// The P/Invoke declarations below match the vJoy SDK v2.1.8+ public API.
/// </summary>
public sealed class VJoyController : IVirtualController
{
    public string Name => $"vJoy Device {_deviceId}";
 
    public event Action<string>? ErrorOccurred;
 
    private readonly uint _deviceId;
    private bool          _connected;
    private bool          _disposed;
 
    public VJoyController(uint deviceId = 1)
    {
        _deviceId = deviceId;
    }
 
    public bool Connect(out string error)
    {
        error = string.Empty;
 
        if (!vJoyEnabled())
        {
            error = "vJoy driver is not enabled. Please install vJoy and configure Device 1.";
            return false;
        }
 
        // Check version compatibility
        uint dllVer = 0, drvVer = 0;
        if (!DriverMatch(ref dllVer, ref drvVer))
        {
            error = $"vJoy DLL version ({dllVer}) does not match driver version ({drvVer}). " +
                    "Please reinstall vJoy.";
            return false;
        }
 
        var status = GetVJDStatus(_deviceId);
        switch (status)
        {
            case VjdStat.VJD_STAT_OWN:
                // We already own it — shouldn't happen on first connect but handle gracefully
                break;
 
            case VjdStat.VJD_STAT_FREE:
                if (!AcquireVJD(_deviceId))
                {
                    error = $"Failed to acquire vJoy Device {_deviceId}. " +
                            "Is another application using it?";
                    return false;
                }
                break;
 
            case VjdStat.VJD_STAT_BUSY:
                error = $"vJoy Device {_deviceId} is in use by another application.";
                return false;
 
            case VjdStat.VJD_STAT_MISS:
                error = $"vJoy Device {_deviceId} does not exist. " +
                        "Open 'Configure vJoy' and enable Device 1 with the required axes and buttons.";
                return false;
 
            default:
                error = $"vJoy Device {_deviceId} is in an unknown state ({status}).";
                return false;
        }
 
        ResetVJD(_deviceId);
        _connected = true;
        return true;
    }
 
    public void Disconnect()
    {
        if (!_connected) return;
        RelinquishVJD(_deviceId);
        _connected = false;
    }
 
    public void Submit(in VJoyReport report)
    {
        if (!_connected) return;
 
        var pos = new JoystickPosition
        {
            bDevice = (byte)_deviceId,
            wAxisX  = (int)report.AxisX,
            wAxisY  = (int)report.AxisY,
            wAxisZ  = (int)report.AxisZ,
            wAxisXRot = (int)report.AxisRx,
            wAxisYRot = (int)report.AxisRy,
            wAxisZRot = (int)report.AxisRz,
            lButtons  = (int)report.Buttons,
            bHats     = report.Pov,
        };
 
        if (!UpdateVJD(_deviceId, ref pos))
        {
            // Device may have been unplugged or driver reset; signal error
            ErrorOccurred?.Invoke($"vJoy UpdateVJD failed for Device {_deviceId}.");
            _connected = false;
        }
    }
 
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
 
    // ── P/Invoke — vJoyInterface.dll ─────────────────────────────────────────
 
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool vJoyEnabled();
 
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DriverMatch(ref uint dllVer, ref uint drvVer);
 
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern VjdStat GetVJDStatus(uint rID);
 
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AcquireVJD(uint rID);
 
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void RelinquishVJD(uint rID);
 
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ResetVJD(uint rID);
 
    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateVJD(uint rID, ref JoystickPosition pData);
 
    private enum VjdStat
    {
        VJD_STAT_OWN  = 0,
        VJD_STAT_FREE = 1,
        VJD_STAT_BUSY = 2,
        VJD_STAT_MISS = 3,
        VJD_STAT_UNKN = 4,
    }
 
    // Must exactly match the JOYSTICK_POSITION struct in the vJoy SDK.
    [StructLayout(LayoutKind.Sequential)]
    private struct JoystickPosition
    {
        public byte  bDevice;
        public int   wThrottle;
        public int   wRudder;
        public int   wAileron;
        public int   wAxisX;
        public int   wAxisY;
        public int   wAxisZ;
        public int   wAxisXRot;
        public int   wAxisYRot;
        public int   wAxisZRot;
        public int   wSlider;
        public int   wDial;
        public int   wWheel;
        public int   wAxisVX;
        public int   wAxisVY;
        public int   wAxisVZ;
        public int   wAxisVBRX;
        public int   wAxisVBRY;
        public int   wAxisVBRZ;
        public int   lButtons;   // buttons 1–32 bitmask
        public uint  bHats;      // first continuous POV hat (tenths of degree, 0xFFFFFFFF = neutral)
        public uint  bHatsEx1;
        public uint  bHatsEx2;
        public uint  bHatsEx3;
    }
}
