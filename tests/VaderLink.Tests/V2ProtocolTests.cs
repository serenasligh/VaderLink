using System;
using System.Buffers.Binary;
using VaderLink.Hid;
using VaderLink.Mapping;
using VaderLink.Model;
using Xunit;
 
namespace VaderLink.Tests;
 
/// <summary>
/// Unit tests for V2Protocol byte-level parsing.
/// These tests run on any platform (no Windows-specific APIs used).
/// </summary>
public class V2ProtocolTests
{
    // ── Helper: build a minimal valid V2 input report ─────────────────────────
 
    private static byte[] MakeReport(
        short lx = 0, short ly = 0,
        short rx = 0, short ry = 0,
<<<<<<< claude/vader-keysticks-integration-qgeXa
        byte  faceDpad  = 0,
        byte  misc      = 0,
        byte  extra     = 0,
        byte  system    = 0,
        byte  ltrigger  = 0,
        byte  rtrigger  = 0,
        short gyroX     = 0, short gyroY = 0, short gyroZ = 0,
        short accelX    = 0, short accelY = 0, short accelZ = 0)
=======
        byte faceDpad = 0,
        byte misc     = 0,
        byte extra    = 0,
        byte system   = 0)
>>>>>>> main
    {
        var data = new byte[32];
        data[0] = 0x5A;  // Magic1
        data[1] = 0xA5;  // Magic2
        data[2] = 0xEF;  // ReportTypeInput
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(3),  lx);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(5),  ly);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(7),  rx);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(9),  ry);
        data[11] = faceDpad;
        data[12] = misc;
        data[13] = extra;
        data[14] = system;
<<<<<<< claude/vader-keysticks-integration-qgeXa
        data[15] = ltrigger;
        data[16] = rtrigger;
        // SDL3 layout: gyro X/Z/Y then accel X/Z/Y (re-ordered to named fields in parser)
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(17), gyroX);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(19), gyroZ);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(21), gyroY);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(23), accelX);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(25), accelZ);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(27), accelY);
=======
>>>>>>> main
        return data;
    }
 
    // ── TryParseInputReport ───────────────────────────────────────────────────
 
    [Fact]
    public void Returns_null_when_magic_bytes_are_wrong()
    {
        var data = MakeReport();
        data[0] = 0x00; // corrupt magic
        Assert.Null(V2Protocol.TryParseInputReport(data));
    }
 
    [Fact]
    public void Returns_null_when_report_type_is_not_input()
    {
        var data = MakeReport();
        data[2] = 0x01; // not 0xEF
        Assert.Null(V2Protocol.TryParseInputReport(data));
    }
 
    [Fact]
    public void Returns_null_when_buffer_is_too_short()
    {
        var data = new byte[10]; // shorter than MinReportLength
        Assert.Null(V2Protocol.TryParseInputReport(data));
    }
 
    [Fact]
    public void Parses_valid_report_without_any_buttons_pressed()
    {
        var data  = MakeReport();
        var state = V2Protocol.TryParseInputReport(data);
 
        Assert.NotNull(state);
        Assert.False(state!.Value.ButtonA);
        Assert.False(state!.Value.ButtonB);
        Assert.False(state!.Value.ButtonC);
        Assert.False(state!.Value.DPadUp);
        Assert.Equal(0, state!.Value.LeftStickX);
        Assert.Equal(0, state!.Value.LeftStickY);
    }
 
    // ── Face buttons (high nibble of d[11]) ───────────────────────────────────
 
    [Theory]
    [InlineData(0x10, true,  false, false, false)]  // A
    [InlineData(0x20, false, true,  false, false)]  // B
    [InlineData(0x80, false, false, true,  false)]  // X
    [InlineData(0x01, false, false, false, false)]  // D-pad Up should not set face buttons
    public void Face_buttons_decode_correctly(
        byte faceDpad,
        bool expectA, bool expectB, bool expectX, bool expectY)
    {
        var state = V2Protocol.TryParseInputReport(MakeReport(faceDpad: faceDpad))!.Value;
        Assert.Equal(expectA, state.ButtonA);
        Assert.Equal(expectB, state.ButtonB);
        Assert.Equal(expectX, state.ButtonX);
        Assert.Equal(expectY, state.ButtonY);
    }
 
    [Fact]
    public void Button_Y_is_in_misc_byte_not_face_byte()
    {
        // Y is bit 0x01 of d[12], not d[11]
        var state = V2Protocol.TryParseInputReport(MakeReport(misc: 0x01))!.Value;
        Assert.True(state.ButtonY);
        Assert.False(state.ButtonA);
        Assert.False(state.ButtonB);
        Assert.False(state.ButtonX);
    }
 
    // ── D-pad (low nibble of d[11]) ───────────────────────────────────────────
 
    [Theory]
    [InlineData(0x01, true,  false, false, false)]  // Up
    [InlineData(0x02, false, true,  false, false)]  // Right
    [InlineData(0x04, false, false, true,  false)]  // Down
    [InlineData(0x08, false, false, false, true )]  // Left
    [InlineData(0x03, true,  true,  false, false)]  // Up+Right (diagonal)
    public void DPad_decodes_correctly(
        byte faceDpad,
        bool expectUp, bool expectRight, bool expectDown, bool expectLeft)
    {
        var state = V2Protocol.TryParseInputReport(MakeReport(faceDpad: faceDpad))!.Value;
        Assert.Equal(expectUp,    state.DPadUp);
        Assert.Equal(expectRight, state.DPadRight);
        Assert.Equal(expectDown,  state.DPadDown);
        Assert.Equal(expectLeft,  state.DPadLeft);
    }
 
    // ── Extra buttons (d[13]) ─────────────────────────────────────────────────
 
    [Theory]
    [InlineData(0x01, nameof(ControllerState.ButtonC))]
    [InlineData(0x02, nameof(ControllerState.ButtonZ))]
    [InlineData(0x04, nameof(ControllerState.ButtonM1))]
    [InlineData(0x08, nameof(ControllerState.ButtonM2))]
    [InlineData(0x10, nameof(ControllerState.ButtonM3))]
    [InlineData(0x20, nameof(ControllerState.ButtonM4))]
    [InlineData(0x40, nameof(ControllerState.ButtonLM))]
    [InlineData(0x80, nameof(ControllerState.ButtonRM))]
    public void Extra_buttons_each_map_to_correct_bit(byte mask, string propertyName)
    {
        var state = V2Protocol.TryParseInputReport(MakeReport(extra: mask))!.Value;
 
        bool value = propertyName switch
        {
            nameof(ControllerState.ButtonC)  => state.ButtonC,
            nameof(ControllerState.ButtonZ)  => state.ButtonZ,
            nameof(ControllerState.ButtonM1) => state.ButtonM1,
            nameof(ControllerState.ButtonM2) => state.ButtonM2,
            nameof(ControllerState.ButtonM3) => state.ButtonM3,
            nameof(ControllerState.ButtonM4) => state.ButtonM4,
            nameof(ControllerState.ButtonLM) => state.ButtonLM,
            nameof(ControllerState.ButtonRM) => state.ButtonRM,
            _ => throw new ArgumentException(propertyName),
        };
 
        Assert.True(value, $"{propertyName} should be true when bit {mask:X2} is set");
    }
 
    [Fact]
    public void All_extra_buttons_set_when_all_bits_set()
    {
        var state = V2Protocol.TryParseInputReport(MakeReport(extra: 0xFF))!.Value;
        Assert.True(state.ButtonC);
        Assert.True(state.ButtonZ);
        Assert.True(state.ButtonM1);
        Assert.True(state.ButtonM2);
        Assert.True(state.ButtonM3);
        Assert.True(state.ButtonM4);
        Assert.True(state.ButtonLM);
        Assert.True(state.ButtonRM);
    }
 
    // ── Analog sticks ─────────────────────────────────────────────────────────
 
    [Theory]
    [InlineData(0,      0)]
    [InlineData(32767,  32767)]
    [InlineData(-32768, -32768)]
    [InlineData(16000,  16000)]
    public void Left_stick_X_axis_roundtrips(short value, short expected)
    {
        var state = V2Protocol.TryParseInputReport(MakeReport(lx: value))!.Value;
        Assert.Equal(expected, state.LeftStickX);
    }
 
    [Theory]
    [InlineData(0,      0)]
    [InlineData(32767,  32767)]
    [InlineData(-32768, -32768)]
    public void Right_stick_Y_axis_roundtrips(short value, short expected)
    {
        var state = V2Protocol.TryParseInputReport(MakeReport(ry: value))!.Value;
        Assert.Equal(expected, state.RightStickY);
    }
 
    // ── Report-ID shift ───────────────────────────────────────────────────────
 
    [Fact]
    public void Handles_report_ID_prefix_transparently()
    {
        // Some HID stacks prepend a report-ID byte.
        // Insert 0x01 (a plausible report ID) before the magic bytes.
        var inner = MakeReport(extra: 0x04); // M1 pressed
        var withId = new byte[inner.Length + 1];
        withId[0] = 0x01; // report ID
        inner.CopyTo(withId, 1);
 
        var state = V2Protocol.TryParseInputReport(withId);
        Assert.NotNull(state);
        Assert.True(state!.Value.ButtonM1);
    }
 
    // ── Mapper axis scaling ───────────────────────────────────────────────────
 
    [Theory]
    [InlineData(0,      16384)]   // centre → vJoy centre
    [InlineData(32767,  32767)]   // max → vJoy max
    [InlineData(-32768, 1)]       // min → vJoy min
    public void Stick_axis_scales_to_vJoy_range(short raw, long expected)
    {
        long actual = Mapper.ScaleStickAxis(raw);
        // Allow ±1 for rounding
        Assert.InRange(actual, expected - 1, expected + 1);
    }
 
    [Theory]
    [InlineData(0,   16384)]   // trigger off  → vJoy centre (neutral; Keysticks sees "not pressed")
    [InlineData(255, 32767)]   // trigger full  → vJoy max
    [InlineData(128, 24608)]   // trigger half  → approx midpoint (16384 + 8224)
    public void Trigger_axis_scales_to_vJoy_range(byte raw, long expected)
    {
        long actual = Mapper.ScaleTriggerAxis(raw);
        Assert.InRange(actual, expected - 2, expected + 2);
    }
 
    // ── Mapper POV hat ────────────────────────────────────────────────────────
 
    [Theory]
    [InlineData(true,  false, false, false, 0u)]          // N
    [InlineData(true,  true,  false, false, 4500u)]       // NE
    [InlineData(false, true,  false, false, 9000u)]       // E
    [InlineData(false, false, false, false, 0xFFFFFFFFu)] // neutral
    public void Pov_hat_computes_correct_direction(
        bool up, bool right, bool down, bool left, uint expected)
    {
        Assert.Equal(expected, Mapper.ComputePov(up, right, down, left));
    }
 
    // ── Mapper full round-trip ────────────────────────────────────────────────
 
    [Fact]
    public void Mapper_sets_correct_button_bits_for_all_standard_buttons()
    {
        var state = new ControllerState
        {
            ButtonA = true,    // bit 0  → vJoy button 1
            ButtonB = true,    // bit 1  → vJoy button 2
            ButtonLB = true,   // bit 4  → vJoy button 5
            ButtonC = true,    // bit 11 → vJoy button 12
            ButtonRM = true,   // bit 18 → vJoy button 19
        };
 
        var report = Mapper.Map(in state, leftTrigger: 0, rightTrigger: 0);
 
        Assert.True((report.Buttons & (1u << 0))  != 0,  "Button A");
        Assert.True((report.Buttons & (1u << 1))  != 0,  "Button B");
        Assert.True((report.Buttons & (1u << 4))  != 0,  "LB");
        Assert.True((report.Buttons & (1u << 11)) != 0,  "C");
        Assert.True((report.Buttons & (1u << 18)) != 0,  "RM");
 
        // Unset buttons should be 0
        Assert.True((report.Buttons & (1u << 2))  == 0,  "X should be off");
        Assert.True((report.Buttons & (1u << 12)) == 0,  "Z should be off");
    }
<<<<<<< claude/vader-keysticks-integration-qgeXa

    [Fact]
    public void Trigger_round_trip_through_mapper()
    {
        // Fully pressed triggers should map to vJoy axis minimum (1).
        var state = new ControllerState { LeftTrigger = 255, RightTrigger = 255 };
        var report = Mapper.Map(in state);
        Assert.Equal(1L, report.AxisZ);
        Assert.Equal(1L, report.AxisRz);

        // Released triggers should map to vJoy axis centre (16384).
        var released = new ControllerState { LeftTrigger = 0, RightTrigger = 0 };
        var rReport = Mapper.Map(in released);
        Assert.Equal(16384L, rReport.AxisZ);
        Assert.Equal(16384L, rReport.AxisRz);
    }

    // ── Motion sensor parsing ─────────────────────────────────────────────────

    [Fact]
    public void Motion_sensors_parse_from_bytes_17_to_28()
    {
        var state = V2Protocol.TryParseInputReport(MakeReport(
            gyroX: 1000, gyroY: -2000, gyroZ: 500,
            accelX: -100, accelY: 200, accelZ: 300))!.Value;

        Assert.Equal((short)1000,  state.GyroX);
        Assert.Equal((short)-2000, state.GyroY);
        Assert.Equal((short)500,   state.GyroZ);
        Assert.Equal((short)-100,  state.AccelX);
        Assert.Equal((short)200,   state.AccelY);
        Assert.Equal((short)300,   state.AccelZ);
    }

    [Fact]
    public void Motion_sensors_at_rest_produce_centred_vJoy_axes()
    {
        // When all sensors read zero (centred at rest), all motion axes should be at vJoy centre.
        var state = new ControllerState(); // all zeros
        var report = Mapper.MotionMap(in state);

        Assert.Equal(16384L, report.AxisX);   // Gyro X
        Assert.Equal(16384L, report.AxisY);   // Gyro Y
        Assert.Equal(16384L, report.AxisZ);   // Gyro Z
        Assert.Equal(16384L, report.AxisRx);  // Accel X
        Assert.Equal(16384L, report.AxisRy);  // Accel Y
        Assert.Equal(16384L, report.AxisRz);  // Accel Z
        Assert.Equal(0u,     report.Buttons);
        Assert.Equal(Mapper.PovNeutral, report.Pov);
    }

    [Fact]
    public void Motion_sensors_at_max_produce_max_vJoy_axes()
    {
        var state = new ControllerState
        {
            GyroX = 32767, GyroY = 32767, GyroZ = 32767,
            AccelX = 32767, AccelY = 32767, AccelZ = 32767,
        };
        var report = Mapper.MotionMap(in state);

        // All axes should be at or near vJoy maximum (32767).
        Assert.InRange(report.AxisX, 32766, 32767);
        Assert.InRange(report.AxisRz, 32766, 32767);
    }

    [Fact]
    public void Motion_sensors_at_min_produce_min_vJoy_axes()
    {
        var state = new ControllerState
        {
            GyroX = -32768, GyroY = -32768, GyroZ = -32768,
            AccelX = -32768, AccelY = -32768, AccelZ = -32768,
        };
        var report = Mapper.MotionMap(in state);

        Assert.Equal(1L, report.AxisX);
        Assert.Equal(1L, report.AxisRz);
    }
=======
>>>>>>> main
}
