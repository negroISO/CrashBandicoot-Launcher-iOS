using GameController;
using RecompOne.Runtime.Hardware;

namespace CrashBandicoot.IOSRuntime;

internal static class IOSGamepad
{
    const float TriggerThreshold = 0.35f;
    static string? _lastName;
    static ushort _lastPressed;

    // Headless device validation: with CRASH_IOS_AUTOPRESS=1 and no physical
    // controller, synthesize Start (leave title) then Cross (close the host
    // pause overlay) so real gameplay/HUD can be reached without input hardware.
    static readonly bool _autoPress =
        Environment.GetEnvironmentVariable("CRASH_IOS_AUTOPRESS") == "1";
    static long _autoPressStart = -1;

    public static bool MenuActive { get; set; }
    public static event Action? StartPressed;
    public static event Action? CrossPressed;
    public static event Action? SelectPressed;

    static ushort AutoPressButtons()
    {
        if (_autoPressStart < 0) _autoPressStart = Environment.TickCount64;
        var t = (Environment.TickCount64 - _autoPressStart) / 1000.0;
        if (t is >= 2.0 and < 2.8) return Controller.Start;
        if (t is >= 3.5 and < 4.1) return Controller.Cross;
        return 0;
    }

    public static void Publish(Action<string>? setStatus = null)
    {
        var controller = GCController.Controllers?
            .FirstOrDefault(item => item.ExtendedGamepad is not null);
        if (controller?.ExtendedGamepad is not { } pad)
        {
            if (_autoPress)
            {
                var synthetic = AutoPressButtons();
                Controller.SetPhysicalPadState(synthetic, 0x80, 0x80, 0x80, 0x80, true);
                PublishEdges(synthetic);
                if (MenuActive)
                    Controller.SetPhysicalPadState(0, 0x80, 0x80, 0x80, 0x80, false);
                return;
            }
            Controller.SetPhysicalPadState(0, 0x80, 0x80, 0x80, 0x80, false);
            return;
        }

        var pressed = (ushort)(
            Button(pad.ButtonA, Controller.Cross) |
            Button(pad.ButtonB, Controller.Circle) |
            Button(pad.ButtonX, Controller.Square) |
            Button(pad.ButtonY, Controller.Triangle) |
            Button(pad.ButtonMenu, Controller.Start) |
            Button(pad.ButtonOptions, Controller.Select) |
            Button(pad.LeftShoulder, Controller.L1) |
            Button(pad.RightShoulder, Controller.R1) |
            Trigger(pad.LeftTrigger, Controller.L2) |
            Trigger(pad.RightTrigger, Controller.R2) |
            Button(pad.LeftThumbstickButton, Controller.L3) |
            Button(pad.RightThumbstickButton, Controller.R3) |
            Button(pad.DPad.Up, Controller.Up) |
            Button(pad.DPad.Down, Controller.Down) |
            Button(pad.DPad.Left, Controller.Left) |
            Button(pad.DPad.Right, Controller.Right));

        // GameController's Y axes are positive-up, while the shared runtime's
        // PS1 axes follow SDL's positive-down convention.
        Controller.SetPhysicalPadState(
            pressed,
            AxisByte(pad.LeftThumbstick.XAxis.Value),
            AxisByte(-pad.LeftThumbstick.YAxis.Value),
            AxisByte(pad.RightThumbstick.XAxis.Value),
            AxisByte(-pad.RightThumbstick.YAxis.Value),
            true);

        PublishEdges(pressed);

        if (MenuActive)
        {
            Controller.SetPhysicalPadState(0, 0x80, 0x80, 0x80, 0x80, false);
            return;
        }

        if (setStatus is not null &&
            controller.VendorName is { } name && name != _lastName)
        {
            _lastName = name;
            setStatus($"Gamepad connected: {name}");
        }
    }

    static void PublishEdges(ushort pressed)
    {
        if ((pressed & Controller.Start) != (_lastPressed & Controller.Start) &&
            (pressed & Controller.Start) != 0)
            StartPressed?.Invoke();
        if ((pressed & Controller.Select) != (_lastPressed & Controller.Select) &&
            (pressed & Controller.Select) != 0)
            SelectPressed?.Invoke();
        if ((pressed & Controller.Cross) != (_lastPressed & Controller.Cross) &&
            (pressed & Controller.Cross) != 0)
            CrossPressed?.Invoke();
        _lastPressed = pressed;
    }

    static ushort Button(GCControllerButtonInput? input, ushort bit) =>
        input is { IsPressed: true } ? bit : (ushort)0;

    static ushort Trigger(GCControllerButtonInput? input, ushort bit) =>
        input is { Value: > TriggerThreshold } ? bit : (ushort)0;

    static byte AxisByte(float value) =>
        (byte)Math.Clamp(MathF.Round((Math.Clamp(value, -1f, 1f) + 1f) * 127.5f), 0f, 255f);
}
