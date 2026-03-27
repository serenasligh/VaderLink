using HidSharp;
 
namespace VaderLink.Hid;
 
/// <summary>
/// Wraps a HidSharp HidDevice for the Vader 5 Pro's vendor HID sideband interface
/// (VID 0x37D7, PID 0x2401). Handles open/close and raw report I/O.
/// </summary>
public sealed class VaderHidDevice : IDisposable
{
    private HidDevice?  _device;
    private HidStream?  _stream;
    private bool        _disposed;
 
    // Buffer size: 64 bytes is sufficient; the controller sends 32-byte reports
    // but HidSharp may return the full 64-byte USB packet.
    private readonly byte[] _readBuffer = new byte[64];
 
    /// <summary>
    /// Finds and opens the Vader 5 Pro vendor HID interface.
    /// Returns true on success. Throws nothing — errors are captured in <paramref name="error"/>.
    /// </summary>
    public bool TryOpen(out string error)
    {
        error = string.Empty;
 
        var list = DeviceList.Local;
        var devices = list.GetHidDevices(V2Protocol.VendorId, V2Protocol.ProductId).ToList();
 
        if (devices.Count == 0)
        {
            error = $"No HID device found with VID 0x{V2Protocol.VendorId:X4} / PID 0x{V2Protocol.ProductId:X4}. " +
                    "Is the Vader 5 Pro connected via USB dongle or cable?";
            return false;
        }
 
        // If multiple interfaces are exposed, prefer the one whose usage page suggests
        // a gamepad (Generic Desktop, usage page 0x01). Fall back to the first device.
        HidDevice? chosen = devices.FirstOrDefault(d =>
        {
            try { return d.GetReportDescriptor().DeviceItems.Any(); }
            catch { return false; }
        }) ?? devices[0];
 
        try
        {
            var stream = chosen.Open();
            stream.ReadTimeout  = 150; // ms — short enough to allow heartbeat checks
            stream.WriteTimeout = 500;
            _device = chosen;
            _stream = stream;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to open vendor HID device: {ex.Message}. " +
                    "Try running VaderLink as Administrator if the error persists.";
            return false;
        }
    }
 
    /// <summary>
    /// Blocking read. Returns the number of bytes read, or -1 on timeout.
    /// Throws <see cref="IOException"/> if the device is disconnected.
    /// </summary>
    public int Read(out ReadOnlySpan<byte> data)
    {
        if (_stream is null) throw new InvalidOperationException("Device not open.");
        try
        {
            int n = _stream.Read(_readBuffer, 0, _readBuffer.Length);
            data = _readBuffer.AsSpan(0, n);
            return n;
        }
        catch (TimeoutException)
        {
            data = ReadOnlySpan<byte>.Empty;
            return -1;
        }
    }
 
    /// <summary>Sends a command to the controller as an HID output report.</summary>
    public void Write(byte[] command)
    {
        if (_stream is null) throw new InvalidOperationException("Device not open.");
        _stream.Write(command);
    }
 
    public void Close()
    {
        _stream?.Close();
        _stream  = null;
        _device  = null;
    }
 
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }
}