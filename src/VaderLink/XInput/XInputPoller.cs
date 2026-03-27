using System.Runtime.InteropServices;

namespace VaderLink.XInput;

/// <summary>
/// Polls the Vader 5 Pro's XInput interface at ~125 Hz to read trigger values.
/// Trigger axes are NOT present in the vendor HID V2 report; they are only
/// available via the XInput (VID 0x045E / PID 0x028E) interface.
/// </summary>
public sealed class XInputPoller : IDisposable
{
    // These are read by the Mapper on the output thread — volatile is sufficient
    // since we only need acquire/release semantics, not atomicity of the pair.
    public volatile byte LeftTrigger;
    public volatile byte RightTrigger;

    private Thread?                  _thread;
    private CancellationTokenSource? _cts;
    private bool                     _disposed;

    // Poll at 125 Hz (8 ms) — fast enough that trigger input feels responsive
    // without generating excessive CPU load.
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(8);

    public void Start()
    {
        if (_thread is not null) return;
        _cts    = new CancellationTokenSource();
        _thread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name         = "XInputPoller",
        };
        _thread.Start();
    }

    public void Stop()
    {
        _cts?.Cancel();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    private void PollLoop()
    {
        var token = _cts!.Token;

        while (!token.IsCancellationRequested)
        {
            var result = XInputGetState(0, out var state);

            if (result == 0) // ERROR_SUCCESS
            {
                LeftTrigger  = state.Gamepad.LeftTrigger;
                RightTrigger = state.Gamepad.RightTrigger;
            }
            else
            {
                // Controller not connected via XInput (e.g., dongle out of range)
                LeftTrigger  = 0;
                RightTrigger = 0;
            }

            try { Task.Delay(PollInterval, token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { break; }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint       PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte   LeftTrigger;
        public byte   RightTrigger;
        public short  LeftThumbX;
        public short  LeftThumbY;
        public short  RightThumbX;
        public short  RightThumbY;
    }
}
