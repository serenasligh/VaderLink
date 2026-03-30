namespace VaderLink.Model;
 
/// <summary>
/// Immutable snapshot of the Vader 5 Pro's input state, parsed from a single V2 HID report.
/// Passed across threads via Channel; being a record struct avoids heap allocation.
/// </summary>
public readonly record struct ControllerState
{
    // ── Analog axes (raw int16, -32768..32767, centre = 0) ───────────────────
    public short LeftStickX  { get; init; }
    public short LeftStickY  { get; init; }
    public short RightStickX { get; init; }
    public short RightStickY { get; init; }
 
    // ── Standard face / shoulder / system buttons ────────────────────────────
    public bool ButtonA     { get; init; }
    public bool ButtonB     { get; init; }
    public bool ButtonX     { get; init; }
    public bool ButtonY     { get; init; }
    public bool ButtonLB    { get; init; }
    public bool ButtonRB    { get; init; }
    public bool ButtonBack  { get; init; }
    public bool ButtonStart { get; init; }
    public bool ButtonL3    { get; init; }
    public bool ButtonR3    { get; init; }
    public bool ButtonGuide { get; init; }
    public bool ButtonFn    { get; init; }
 
    // ── D-Pad ─────────────────────────────────────────────────────────────────
    public bool DPadUp    { get; init; }
    public bool DPadRight { get; init; }
    public bool DPadDown  { get; init; }
    public bool DPadLeft  { get; init; }
 
    // ── Extra buttons (only available via vendor HID sideband) ────────────────
    public bool ButtonC  { get; init; }
    public bool ButtonZ  { get; init; }
    public bool ButtonM1 { get; init; }
    public bool ButtonM2 { get; init; }
    public bool ButtonM3 { get; init; }
    public bool ButtonM4 { get; init; }
    public bool ButtonLM { get; init; }
    public bool ButtonRM { get; init; }
 
    // ── System buttons (d[14] of V2 report) ─────────────────────────────────
    // Bit 0x01 = Fn / Circle button (confirmed by testing: appears as vJoy button 11).
    // Bit 0x02 = Guide / Home button (typically intercepted by Windows Xbox services).
    // These replace the earlier misnamed ButtonGuide / ButtonFn pair.
 
    // ── Battery (populated by the reader from device-info responses) ──────────
    public byte BatteryPercent { get; init; }
    public bool IsCharging     { get; init; }
 
    // ── Timestamp for ordering / latency diagnostics ─────────────────────────
    public long TimestampTicks { get; init; }
}
