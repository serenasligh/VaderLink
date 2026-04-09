using System.Buffers.Binary;
using VaderLink.Model;
 
namespace VaderLink.Hid;
 
/// <summary>
/// Constants and parsing logic for the Flydigi V2 HID protocol used by the Vader 5 Pro.
/// Byte offsets are sourced from SDL3's SDL_hidapi_flydigi.c (authoritative open-source reference).
/// </summary>
public static class V2Protocol
{
    // ── USB device identity ───────────────────────────────────────────────────
    public const int VendorId  = 0x37D7;
    public const int ProductId = 0x2401;
 
    // ── Report framing ────────────────────────────────────────────────────────
    public const byte Magic1          = 0x5A;
    public const byte Magic2          = 0xA5;
    public const byte ReportTypeInput = 0xEF;
    public const int  MinReportLength = 32;
 
    // ── Device model ID returned in device-info response ─────────────────────
    public const byte ModelIdVader5Pro = 130;
 
    // ── Commands (written via HID output/feature reports) ────────────────────
 
    /// <summary>Sent once on connect and every 30 s as a heartbeat to keep the vendor interface active.</summary>
    public static readonly byte[] AcquireCmd    = [0x03, 0x5A, 0xA5, 0x1C, 0x02, 0x01];
 
    /// <summary>Released on clean disconnect so Flydigi Space Station can reclaim the interface.</summary>
    public static readonly byte[] ReleaseCmd    = [0x03, 0x5A, 0xA5, 0x1C, 0x02, 0x00];
 
    /// <summary>Request device info (model ID, firmware version, battery).</summary>
    public static readonly byte[] DeviceInfoCmd = [0x03, 0x5A, 0xA5, 0x01, 0x02, 0x00];
 
    /// <summary>Haptic rumble command. Motors accept 0 (off) – 255 (full).</summary>
    public static byte[] RumbleCmd(byte lowMotor, byte highMotor) =>
        [0x03, 0x5A, 0xA5, 0x12, 0x06, lowMotor, highMotor, 0x00, 0x00, 0x00];
 
    // ── Input report parsing ──────────────────────────────────────────────────
 
    /// <summary>
    /// Attempts to parse a raw HID read buffer into a <see cref="ControllerState"/>.
    /// Returns <c>null</c> if the buffer does not contain a valid V2 input report.
    /// </summary>
    /// <param name="raw">Raw bytes exactly as returned by HidSharp's Read().</param>
    /// <param name="batteryPercent">Current battery level to carry forward into the state.</param>
    /// <param name="isCharging">Current charging status to carry forward.</param>
    public static ControllerState? TryParseInputReport(
        ReadOnlySpan<byte> raw,
        byte batteryPercent = 0,
        bool isCharging = false)
    {
        // Some USB HID stacks prepend a report-ID byte even when there is only one
        // report.  If data[0] is not the magic byte but data[1] is, shift by one.
        int o = 0;
        if (raw.Length > 1 && raw[0] != Magic1 && raw[1] == Magic1)
            o = 1;
 
        if (raw.Length - o < MinReportLength) return null;
        if (raw[o]     != Magic1)          return null;
        if (raw[o + 1] != Magic2)          return null;
        if (raw[o + 2] != ReportTypeInput) return null;
 
        var d = raw[o..];
 
        // Analog sticks — int16 little-endian, signed, centre = 0
        short lx = BinaryPrimitives.ReadInt16LittleEndian(d[3..5]);
        short ly = BinaryPrimitives.ReadInt16LittleEndian(d[5..7]);
        short rx = BinaryPrimitives.ReadInt16LittleEndian(d[7..9]);
        short ry = BinaryPrimitives.ReadInt16LittleEndian(d[9..11]);
 
        byte faceDpad = d[11];
        byte misc     = d[12];
        byte extra    = d[13];
        byte system   = d[14];
 
        // ── Face buttons are in the HIGH nibble of d[11] ──────────────────────
        // Note: A is 0x10 (not 0x01) — counterintuitive but confirmed by SDL source.
        bool btnA    = (faceDpad & 0x10) != 0;
        bool btnB    = (faceDpad & 0x20) != 0;
        bool btnBack = (faceDpad & 0x40) != 0;
        bool btnX    = (faceDpad & 0x80) != 0;
 
        // ── D-pad is in the LOW nibble of d[11] ───────────────────────────────
        // 1=Up, 2=Right, 4=Down, 8=Left; diagonals are OR combinations.
        bool dUp    = (faceDpad & 0x01) != 0;
        bool dRight = (faceDpad & 0x02) != 0;
        bool dDown  = (faceDpad & 0x04) != 0;
        bool dLeft  = (faceDpad & 0x08) != 0;
 
        // ── d[12]: remaining standard buttons ────────────────────────────────
        bool btnY     = (misc & 0x01) != 0;
        bool btnStart = (misc & 0x02) != 0;
        bool btnLB    = (misc & 0x04) != 0;
        bool btnRB    = (misc & 0x08) != 0;
        bool btnL3    = (misc & 0x40) != 0;
        bool btnR3    = (misc & 0x80) != 0;
 
        // ── d[13]: all 8 extra buttons ────────────────────────────────────────
        bool btnC  = (extra & 0x01) != 0;
        bool btnZ  = (extra & 0x02) != 0;
        bool btnM1 = (extra & 0x04) != 0;
        bool btnM2 = (extra & 0x08) != 0;
        bool btnM3 = (extra & 0x10) != 0;
        bool btnM4 = (extra & 0x20) != 0;
        bool btnLM = (extra & 0x40) != 0;
        bool btnRM = (extra & 0x80) != 0;
 
        // ── d[14]: system buttons ────────────────────────────────────────────
        // Confirmed by testing: bit 0x01 = Fn/Circle button (appears as vJoy button 11).
        // Bit 0x02 is likely the Guide/Home button but is intercepted by Windows Xbox
        // services and will not be delivered to applications in practice.
        bool btnFn    = (system & 0x01) != 0;
        bool btnGuide = (system & 0x02) != 0;
 
        return new ControllerState
        {
            LeftStickX  = lx, LeftStickY  = ly,
            RightStickX = rx, RightStickY = ry,
 
            ButtonA    = btnA,  ButtonB    = btnB,
            ButtonX    = btnX,  ButtonY    = btnY,
            ButtonBack = btnBack, ButtonStart = btnStart,
            ButtonLB   = btnLB,   ButtonRB    = btnRB,
            ButtonL3   = btnL3,   ButtonR3    = btnR3,
            ButtonGuide = btnGuide, ButtonFn  = btnFn,
 
            DPadUp    = dUp,    DPadRight = dRight,
            DPadDown  = dDown,  DPadLeft  = dLeft,
 
            ButtonC  = btnC,  ButtonZ  = btnZ,
            ButtonM1 = btnM1, ButtonM2 = btnM2,
            ButtonM3 = btnM3, ButtonM4 = btnM4,
            ButtonLM = btnLM, ButtonRM = btnRM,
 
            BatteryPercent = batteryPercent,
            IsCharging     = isCharging,
            TimestampTicks = DateTime.UtcNow.Ticks,
        };
    }
 
    // ── Device-info response parsing ─────────────────────────────────────────
 
    /// <summary>
    /// Tries to parse battery info from a device-info response report.
    /// Returns false if the buffer doesn't look like a device-info response.
    /// </summary>
    public static bool TryParseDeviceInfo(
        ReadOnlySpan<byte> raw,
        out byte modelId,
        out byte batteryPercent,
        out bool isCharging)
    {
        modelId        = 0;
        batteryPercent = 0;
        isCharging     = false;
 
        int o = 0;
        if (raw.Length > 1 && raw[0] != Magic1 && raw[1] == Magic1)
            o = 1;
 
        // Device info response: magic bytes present, report type is NOT an input report,
        // and model ID at d[5] must match the Vader 5 Pro. This prevents misidentifying
        // other non-EF reports (e.g. connection events) as battery updates.
        if (raw.Length - o < 12) return false;
        if (raw[o] != Magic1 || raw[o + 1] != Magic2) return false;
        if (raw[o + 2] == ReportTypeInput) return false;
 
        var d = raw[o..];
        modelId = d[5];
 
        // Only trust battery data when the model ID identifies a Vader 5 Pro.
        if (modelId != ModelIdVader5Pro) return false;
 
        // d[11]: upper nibble = charging status (0=on battery, 1=charging, 2=charged)
        //        lower nibble × 20 = percentage
        byte bat = d[11];
        int chargingStatus = (bat >> 4) & 0x0F;
        isCharging     = chargingStatus == 1;
        batteryPercent = (byte)Math.Min(100, (bat & 0x0F) * 20);
 
        return true;
    }
}
