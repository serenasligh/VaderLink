using HidSharp;
 
namespace VaderLink.Hid;
 
/// <summary>
/// Wraps a HidSharp HidDevice for the Vader 5 Pro's vendor HID sideband interface
/// (VID 0x37D7, PID 0x2401). Handles open/close and raw report I/O.
///
/// The controller exposes multiple HID interfaces under the same VID/PID. Only one
/// of them accepts write commands (the vendor sideband). TryOpen() iterates all
/// enumerated interfaces and returns the first one that successfully accepts a write.
/// </summary>
public sealed class VaderHidDevice : IDisposable
{
    private HidDevice?  _device;
    private HidStream?  _stream;
    private bool        _disposed;
 
    // Buffer size: 64 bytes covers the full USB packet; V2 reports are 32 bytes.
    private readonly byte[] _readBuffer = new byte[64];
 
    /// <summary>
    /// Finds and opens the correct Vader 5 Pro vendor HID interface.
    /// Iterates all interfaces under VID 0x37D7 / PID 0x2401 and picks the first
    /// that accepts a write (validated by sending the V2 acquire command).
    /// Returns true on success. Errors are captured in <paramref name="error"/>.
    /// </summary>
    public bool TryOpen(out string error)
    {
        error = string.Empty;
 
        var devices = DeviceList.Local
            .GetHidDevices(V2Protocol.VendorId, V2Protocol.ProductId)
            .ToList();
 
        if (devices.Count == 0)
        {
            error = $"No HID device found with VID 0x{V2Protocol.VendorId:X4} / " +
                    $"PID 0x{V2Protocol.ProductId:X4}. " +
                    "Is the Vader 5 Pro connected via USB dongle or cable?";
            return false;
        }
 
        // The controller exposes several interfaces (keyboard emulation, mouse emulation,
        // and the vendor data channel). Only the vendor channel accepts output writes.
        // Try each interface in sequence; keep the first one that accepts the acquire command.
        var attemptErrors = new List<string>();
 
        foreach (var candidate in devices)
        {
            HidStream? stream = null;
            try
            {
                stream = candidate.Open();
                stream.ReadTimeout  = 150; // ms — allows heartbeat timer to run between reads
                stream.WriteTimeout = 500;
 
                // Validate that this interface accepts output reports by sending the acquire
                // command. Read-only interfaces will throw here, letting us skip them.
                stream.Write(V2Protocol.AcquireCmd);
 
                // Write succeeded — this is the vendor data channel.
                _device = candidate;
                _stream = stream;
                return true;
            }
            catch (Exception ex)
            {
                stream?.Close();
                attemptErrors.Add($"  [{candidate.DevicePath[..Math.Min(60, candidate.DevicePath.Length)]}]: {ex.Message}");
            }
        }
 
        error = $"Tried {devices.Count} vendor HID interface(s) but none accepted the acquire command.\n" +
                string.Join("\n", attemptErrors) + "\n\n" +
                "Ensure 'Allow third-party apps to take over mappings' is enabled in Flydigi Space Station. " +
                "Try running VaderLink as Administrator if this error persists.";
        return false;
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
