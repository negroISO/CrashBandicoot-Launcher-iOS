using CoreAnimation;
using CoreGraphics;
using Foundation;
using OpenGLES;
using ObjCRuntime;
using Silk.NET.OpenGL;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Hle;
using RecompOne.Runtime.Host;

namespace CrashBandicoot.IOSRuntime;

[Register("CrashIOSGlesView")]
public sealed class GlesView : UIView
{
    public GlesView(CGRect frame) : base(frame)
    {
    }

    [Export("layerClass")]
    public static IntPtr LayerClass() => Class.GetHandle(typeof(CAEAGLLayer));

    EAGLContext? _context;
    uint _colorBuffer;
    uint _framebuffer;

    public GL? GL { get; private set; }
    public GlBackend? Backend { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public void Initialize()
    {
        var layer = (CAEAGLLayer)Layer!;
        layer.Opaque = true;
        _context = new EAGLContext(EAGLRenderingAPI.OpenGLES3);
        if (!EAGLContext.SetCurrentContext(_context))
            throw new InvalidOperationException("Could not make GLES3 context current");

        GL = Silk.NET.OpenGL.GL.GetApi(new EaglNativeContext());
        GlVram.Scale = ConfigManager.View.InternalResolution;
        Backend = new GlBackend(GL);
        var extensions = GL.GetStringS(StringName.Extensions);
        var fetchPath = GlesFramebufferFetchPath.None;
        if (extensions.Contains("GL_EXT_shader_framebuffer_fetch", StringComparison.Ordinal) &&
            Environment.GetEnvironmentVariable("CRASH_IOS_FB_FETCH") == "ext")
            fetchPath = GlesFramebufferFetchPath.Ext;
        else if (extensions.Contains("GL_ARM_shader_framebuffer_fetch", StringComparison.Ordinal) &&
            Environment.GetEnvironmentVariable("CRASH_IOS_FB_FETCH") == "arm")
            fetchPath = GlesFramebufferFetchPath.Arm;
        Backend.InitGl(gles: true, fetchPath);
        if (!Backend.Ready)
            throw new InvalidOperationException("RecompOne GLES backend initialization failed");
        Backend.PreferVramPresentation = true;
        Console.WriteLine(
            $"[CrashIOSGLES] fetch={fetchPath} scale={GlVram.Scale} vramPresent={Backend.PreferVramPresentation}");
        GpuHle.Backend = Backend;
        GpuHle.Active = true;
        GpuHle.NativeResolution = ConfigManager.View.InternalResolution <= 1;
        GpuHle.WideAspect = ConfigManager.View.Widescreen ? 16f / 9f : 0f;
        GpuHle.TextureFilter = ConfigManager.View.TextureFilter;
        GpuHle.TextureFilterStrength = ConfigManager.View.TextureFilterStrength;
        GpuHle.Dedither = ConfigManager.View.Dedither;
        GpuHle.Dejitter = ConfigManager.View.Dejitter;
        GpuHle.PresentNearest = ConfigManager.View.PresentNearest;
        GpuHle.IntegerScale = ConfigManager.View.IntegerScale;
        GpuHle.RefreshWideFov();
        FrameClock.SkipThrottle = false;

        _colorBuffer = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _colorBuffer);
        if (!_context.RenderBufferStorage((nuint)RenderbufferTarget.Renderbuffer, layer))
            throw new InvalidOperationException("Could not bind CAEAGLLayer storage");

        Width = GL.GetRenderbufferParameter(
            RenderbufferTarget.Renderbuffer, RenderbufferParameterName.Width);
        Height = GL.GetRenderbufferParameter(
            RenderbufferTarget.Renderbuffer, RenderbufferParameterName.Height);

        // EAGL does not map framebuffer object zero to the CAEAGLLayer. Create
        // the platform default framebuffer explicitly and pass it to RecompOne
        // during final presentation.
        _framebuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        GL.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            RenderbufferTarget.Renderbuffer, _colorBuffer);
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"EAGL framebuffer incomplete: {status}");
    }

    public bool MakeCurrent() =>
        _context != null && EAGLContext.SetCurrentContext(_context);

    public uint DefaultFramebuffer => _framebuffer;

    public void Present()
    {
        if (_context == null)
            return;
        if (!_context.PresentRenderBuffer((nuint)RenderbufferTarget.Renderbuffer))
            throw new InvalidOperationException("EAGL present failed");
    }
}

public sealed class GlesSurface
{
    public GlesSurface(GlesView view) => View = view;
    public GlesView View { get; }
    public int Width => View.Width;
    public int Height => View.Height;
    public GlBackend Backend => View.Backend ?? throw new InvalidOperationException("GLES surface not initialized");
    public uint DefaultFramebuffer => View.DefaultFramebuffer;

    public bool MakeCurrent() => View.MakeCurrent();

    public void Present() => View.Present();
}
