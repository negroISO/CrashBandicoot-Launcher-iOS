using GameController;
using RecompOne.Runtime.Hardware;

namespace CrashBandicoot.IOSRuntime;

internal static class IOSGamepad
{
    const float TriggerThreshold = 0.35f;
    static string? _lastName;
    static ushort _lastPressed;

    public static bool MenuActive { get; set; }
    public static event Action? StartPressed;
    public static event Action? CrossPressed;
    public static event Action? SelectPressed;

    public static void Publish(Action<string>? setStatus = null)
    {
        var controller = GCController.Controllers?
            .FirstOrDefault(item => item.ExtendedGamepad is not null);
        if (controller?.ExtendedGamepad is not { } pad)
        {
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

        if ((pressed & Controller.Start) != (_lastPressed & Controller.Start))
        {
            Console.WriteLine(
                $"[CrashIOSPad] start={(pressed & Controller.Start) != 0}");
            if ((pressed & Controller.Start) != 0)
                StartPressed?.Invoke();
        }
        if ((pressed & Controller.Select) != (_lastPressed & Controller.Select))
        {
            Console.WriteLine(
                $"[CrashIOSPad] select={(pressed & Controller.Select) != 0}");
            if ((pressed & Controller.Select) != 0)
                SelectPressed?.Invoke();
        }
        if ((pressed & Controller.Cross) != (_lastPressed & Controller.Cross) &&
            (pressed & Controller.Cross) != 0)
        {
            Console.WriteLine(
                $"[CrashIOSPad] cross=true menu={MenuActive}");
            CrossPressed?.Invoke();
        }
        _lastPressed = pressed;

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

    static ushort Button(GCControllerButtonInput? input, ushort bit) =>
        input is { IsPressed: true } ? bit : (ushort)0;

    static ushort Trigger(GCControllerButtonInput? input, ushort bit) =>
        input is { Value: > TriggerThreshold } ? bit : (ushort)0;

    static byte AxisByte(float value) =>
        (byte)Math.Clamp(MathF.Round((Math.Clamp(value, -1f, 1f) + 1f) * 127.5f), 0f, 255f);
}
