namespace VaderLink.Output;
 
/// <summary>
/// Abstraction over a virtual gamepad device (vJoy, ViGEm DS4, etc.).
/// </summary>
public interface IVirtualController : IDisposable
{
    /// <summary>Human-readable name shown in the tray/logs.</summary>
    string Name { get; }
 
    /// <summary>
    /// Opens the virtual device. Returns false (and populates <paramref name="error"/>)
    /// if the required driver is not installed or the device is in use.
    /// </summary>
    bool Connect(out string error);
 
    /// <summary>Releases the virtual device cleanly.</summary>
    void Disconnect();
 
    /// <summary>Submits one frame of input state to the virtual device.</summary>
    void Submit(in VJoyReport report);
 
    /// <summary>Raised (on any thread) when a non-fatal error occurs.</summary>
    event Action<string>? ErrorOccurred;
}
 
/// <summary>
/// All the data VJoyController needs to update a single frame.
/// Long fields match the vJoy SDK's axis type (LONG = 32-bit signed, but we use long
/// for convenience; the P/Invoke struct uses int).
/// </summary>
public readonly struct VJoyReport
{
    public long AxisX  { get; init; }
    public long AxisY  { get; init; }
    public long AxisZ  { get; init; }
    public long AxisRx { get; init; }
    public long AxisRy { get; init; }
    public long AxisRz { get; init; }
 
    /// <summary>Bitmask of buttons 1–32 (bit 0 = button 1).</summary>
    public uint Buttons { get; init; }
 
    /// <summary>Continuous POV hat value in tenths of a degree, or 0xFFFFFFFF for neutral.</summary>
    public uint Pov { get; init; }
}