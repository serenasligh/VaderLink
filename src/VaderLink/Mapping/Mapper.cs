using VaderLink.Model;
using VaderLink.Output;
 
namespace VaderLink.Mapping;
 
/// <summary>
/// Translates a <see cref="ControllerState"/> (plus raw trigger bytes from XInput)
/// into a <see cref="VJoyReport"/> that VJoyController feeds to the vJoy driver.
///
/// vJoy button assignment (Device 1):
///
///   Button  1  → A              Button 13 → Z
///   Button  2  → B              Button 14 → M1
///   Button  3  → X              Button 15 → M2
///   Button  4  → Y              Button 16 → M3
///   Button  5  → LB             Button 17 → M4
///   Button  6  → RB             Button 18 → LM
///   Button  7  → L3             Button 19 → RM
///   Button  8  → R3             Button 20 → DPad Up
///   Button  9  → Start          Button 21 → DPad Right
///   Button 10  → Back           Button 22 → DPad Down
///   Button 11  → Guide          Button 23 → DPad Left
///   Button 12  → C
///
/// Axes:
///   X  = Left Stick X       Rx = Right Stick X
///   Y  = Left Stick Y       Ry = Right Stick Y
///   Z  = Left Trigger       Rz = Right Trigger
///
/// D-Pad is additionally exposed as a continuous POV hat (in tenths of a degree).
/// </summary>
public static class Mapper
{
    // vJoy axis range: 1..32767 with centre at 16384.
    private const long VJoyAxisMin    = 1;
    private const long VJoyAxisMax    = 32767;
    private const long VJoyAxisCentre = 16384;
 
    // POV hat "neutral" sentinel as defined by the vJoy SDK.
    public const uint PovNeutral = 0xFFFFFFFF;
 
    /// <summary>
    /// Converts a signed int16 stick axis (–32768..32767) to the vJoy 1..32767 range.
    /// Pass <paramref name="invert"/> = true for Y axes: HID convention is Y-down = positive,
    /// but most software (including Keysticks) expects Y-up = positive.
    /// </summary>
    public static long ScaleStickAxis(short raw, bool invert = false)
    {
        // Map –32768..32767 → 0..65535, then scale to vJoy 1..32767.
        long shifted = (long)raw + 32768L; // 0..65535
        if (invert) shifted = 65535L - shifted; // flip direction without int16 overflow risk
        return VJoyAxisMin + shifted * (VJoyAxisMax - VJoyAxisMin) / 65535L;
    }
 
    /// <summary>
    /// Converts a trigger byte (0..255) to the vJoy centre..max range (16384..32767).
    /// Trigger at rest (0) sits exactly at axis centre, which Keysticks treats as neutral.
    /// Fully pressed (255) reaches axis maximum. This prevents the "always deflected"
    /// appearance in Keysticks that occurred when rest mapped to axis minimum.
    /// </summary>
    public static long ScaleTriggerAxis(byte raw)
    {
        return VJoyAxisCentre + (long)raw * (VJoyAxisMax - VJoyAxisCentre) / 255L;
    }
 
    /// <summary>
    /// Computes a continuous POV hat value (tenths of a degree, 0..35999)
    /// from the four D-pad direction booleans.
    /// Returns <see cref="PovNeutral"/> when no direction is pressed.
    /// </summary>
    public static uint ComputePov(bool up, bool right, bool down, bool left)
    {
        return (up, right, down, left) switch
        {
            (true,  false, false, false) => 0,        // N
            (true,  true,  false, false) => 4500,     // NE
            (false, true,  false, false) => 9000,     // E
            (false, true,  true,  false) => 13500,    // SE
            (false, false, true,  false) => 18000,    // S
            (false, false, true,  true)  => 22500,    // SW
            (false, false, false, true)  => 27000,    // W
            (true,  false, false, true)  => 31500,    // NW
            _                            => PovNeutral,
        };
    }
 
    /// <summary>
    /// Builds a complete <see cref="VJoyReport"/> from a controller state snapshot
    /// and the latest trigger bytes from the XInput poller.
    /// </summary>
    public static VJoyReport Map(in ControllerState s, byte leftTrigger, byte rightTrigger)
    {
        // ── Axes ───────────────────────────────────────────────────────────────
        long axisX  = ScaleStickAxis(s.LeftStickX);
        long axisY  = ScaleStickAxis(s.LeftStickY,  invert: true); // HID Y-down → Y-up
        long axisRx = ScaleStickAxis(s.RightStickX);
        long axisRy = ScaleStickAxis(s.RightStickY, invert: true); // HID Y-down → Y-up
        long axisZ  = ScaleTriggerAxis(leftTrigger);
        long axisRz = ScaleTriggerAxis(rightTrigger);
 
        // ── Buttons (vJoy uses a bitmask; button N = bit N-1) ─────────────────
        uint buttons = 0;
 
        if (s.ButtonA)     buttons |= 1u << 0;   // Button 1
        if (s.ButtonB)     buttons |= 1u << 1;   // Button 2
        if (s.ButtonX)     buttons |= 1u << 2;   // Button 3
        if (s.ButtonY)     buttons |= 1u << 3;   // Button 4
        if (s.ButtonLB)    buttons |= 1u << 4;   // Button 5
        if (s.ButtonRB)    buttons |= 1u << 5;   // Button 6
        if (s.ButtonL3)    buttons |= 1u << 6;   // Button 7
        if (s.ButtonR3)    buttons |= 1u << 7;   // Button 8
        if (s.ButtonStart) buttons |= 1u << 8;   // Button 9
        if (s.ButtonBack)  buttons |= 1u << 9;   // Button 10
        if (s.ButtonGuide) buttons |= 1u << 10;  // Button 11
        if (s.ButtonC)     buttons |= 1u << 11;  // Button 12
        if (s.ButtonZ)     buttons |= 1u << 12;  // Button 13
        if (s.ButtonM1)    buttons |= 1u << 13;  // Button 14
        if (s.ButtonM2)    buttons |= 1u << 14;  // Button 15
        if (s.ButtonM3)    buttons |= 1u << 15;  // Button 16
        if (s.ButtonM4)    buttons |= 1u << 16;  // Button 17
        if (s.ButtonLM)    buttons |= 1u << 17;  // Button 18
        if (s.ButtonRM)    buttons |= 1u << 18;  // Button 19
        if (s.DPadUp)      buttons |= 1u << 19;  // Button 20
        if (s.DPadRight)   buttons |= 1u << 20;  // Button 21
        if (s.DPadDown)    buttons |= 1u << 21;  // Button 22
        if (s.DPadLeft)    buttons |= 1u << 22;  // Button 23
 
        // ── POV hat ───────────────────────────────────────────────────────────
        uint pov = ComputePov(s.DPadUp, s.DPadRight, s.DPadDown, s.DPadLeft);
 
        return new VJoyReport
        {
            AxisX  = axisX,  AxisY  = axisY,
            AxisRx = axisRx, AxisRy = axisRy,
            AxisZ  = axisZ,  AxisRz = axisRz,
            Buttons = buttons,
            Pov     = pov,
        };
    }
}
