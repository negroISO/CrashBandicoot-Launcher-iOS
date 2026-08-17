#if CRASH_IOS_GENERATED
using RecompOne.Runtime;
using RecompOne.Runtime.Cdrom;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Dispatch;
using RecompOne.Runtime.Memory;
using BiosKernel = RecompOne.Runtime.Bios.Bios;

namespace CrashBandicoot.IOSRuntime;

internal static class GeneratedGame
{
    public static void Run(string cuePath, GlesSurface surface, Action<string> setStatus)
    {
        Runtime.SetPlatformHost(new GlesRuntimeHost(surface, setStatus));
        setStatus("Runtime initialize");
        Runtime.Initialize("CrashBandicoot");
        Runtime.WaitForValidDisc();

        setStatus("Opening disc");
        using var fs = CueFs.Open(cuePath);
        var memory = new PSMemory();
        var cd = new CdController(fs, memory);
        memory.SetCd(cd);
        Dispatcher.Register("main", new Recompiled.MainDispatchTable());

        setStatus("Loading SCUS_949.00");
        cd.LoadToMemory("SCUS_949.00", 0x80010000u, 0x800, 288768);
        Dispatcher.Load("main");

        var cpu = new CpuContext
        {
            GP = 0x00000000u,
            SP = 0x801FFFF0u,
            RA = 0u,
        };
        cpu.FP = cpu.SP;
        Runtime.SetContext(cpu, memory);
        BiosKernel.Init(memory);

        setStatus("Calling recompiled entrypoint");
        Dispatcher.Call(cpu, memory, 0x8003E018u);
        setStatus("Recompiled entrypoint returned");
    }
}
#endif
