using RecompOne.Runtime;
using RecompOne.Runtime.Hle;

namespace CrashBandicoot.IOSRuntime;

internal sealed class GlesRuntimeHost : IRuntimePlatformHost
{
    readonly GlesSurface _surface;
    readonly Action<string> _setStatus;
    readonly IOSAudioOutput _audio = new();
    ulong _frames;
    long _fpsWindow;
    int _fpsFrames;

    public GlesRuntimeHost(GlesSurface surface, Action<string> setStatus)
    {
        _surface = surface;
        _setStatus = setStatus;
    }

    public void Initialize(string title) =>
        _setStatus($"{title}: GLES session starting");

    public void WaitForValidDisc()
    {
    }

    public void Present(Gpu? gpu)
    {
        ++_frames;
        if (gpu == null || !gpu.DisplayEnabled || !_surface.MakeCurrent())
            return;

        var presented = _surface.Backend.PresentDisplay(
            gpu.DisplayX, gpu.DisplayY, gpu.DisplayWidth, gpu.DisplayHeight,
            gpu.Display24Bit, _surface.Width, _surface.Height);
        _surface.Backend.PresentToDefaultFramebuffer(
            _surface.Width, _surface.Height, presented.aspect,
            _surface.DefaultFramebuffer);
        _surface.Present();

        if (_frames == 1U)
            _setStatus("GLES game presentation started");
        else if (_frames == 60U)
            _setStatus("GLES game passed 60 frames");
        else if (_frames == 150U)
            _setStatus(string.Empty);

        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_fpsWindow == 0)
        {
            _fpsWindow = now;
        }
        else
        {
            ++_fpsFrames;
            var elapsed = (now - _fpsWindow) /
                (double)System.Diagnostics.Stopwatch.Frequency;
            if (elapsed >= 2.0)
            {
                Console.WriteLine(
                    $"[CrashIOSGLES] fps={_fpsFrames / elapsed:F2} frames={_frames}");
                _fpsWindow = now;
                _fpsFrames = 0;
            }
        }
    }

    public void AttachAudio(Spu? spu) => _audio.Attach(spu);

    public void SetMasterVolume(float volume) => _audio.SetMasterVolume(volume);

    public void ShowNotice(string message) => _setStatus(message);

    public void Shutdown()
    {
        _audio.Dispose();
        _setStatus("GLES game session ended");
    }
}
