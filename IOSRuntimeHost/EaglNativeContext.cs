using ObjCRuntime;
using Silk.NET.Core.Contexts;

namespace CrashBandicoot.IOSRuntime;

internal sealed class EaglNativeContext : INativeContext
{
    static readonly IntPtr OpenGlesHandle = Dlfcn.dlopen(
        "/System/Library/Frameworks/OpenGLES.framework/OpenGLES", 2);

    public IntPtr GetProcAddress(string proc, int? slot = null) =>
        Dlfcn.dlsym(OpenGlesHandle, proc);

    public bool TryGetProcAddress(string proc, out IntPtr addr, int? slot = null)
    {
        addr = GetProcAddress(proc, slot);
        return addr != IntPtr.Zero;
    }

    public void Dispose()
    {
    }
}
