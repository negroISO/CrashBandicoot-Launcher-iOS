# iOS runtime host

`IOSRuntimeHost` is a UIKit/OpenGL ES host for the RecompOne runtime. The
current milestone is a source-only bring-up target, not a general release.

## Status

- Verified on the iOS 27 arm64 Simulator with Xcode 27 and .NET iOS workload
  27.0.10417.
- The host uses a UIKit scene, `CAEAGLLayer`, an explicit default framebuffer,
  and the shared RecompOne GLES renderer.
- Locally generated game sources from an owned SCUS-94900 dump can be compiled
  directly into the iOS app by MSBuild. This bypasses `AssemblyLoadContext`,
  which cannot load generated assemblies on iOS.
- The generated game has executed on the Simulator and produced live GLES video
  after the host wired `GpuHle.Backend`/`Active`.
- The generated game has also been installed on a physical iPhone 17 Pro Max.
  It reaches the title, gameplay, and pause flow with DualSense input.
- AVAudioEngine streams the shared SPU mixer at 44.1 kHz stereo. The render
  callback fills the complete AudioToolbox request in 512-frame chunks.
- GameController is polled independently on a 60 Hz main-run-loop timer, so
  input remains available while the software FrameClock is paused.
- Start opens a UIKit pause overlay. Touch, Cross, and Start resume; Select or
  the map button queues RecompOne's native Start → Select return-to-map path.

## Public host build

```sh
export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec
export PATH="$DOTNET_ROOT:$PATH"
dotnet workload restore IOSRuntimeHost/IOSRuntimeHost.csproj
dotnet build IOSRuntimeHost/IOSRuntimeHost.csproj \
  -c Debug -f net10.0-ios27.0 \
  -p:RuntimeIdentifier=iossimulator-arm64
```

The public build shows the UIKit/GLES host and does not include generated game
code.

## Local generated game build

First use the existing desktop launcher with your legally supplied disc:

```sh
CrashBandicoot --prepare '/path/to/Crash Bandicoot (USA).cue'
```

Then point MSBuild at the locally generated source directory:

```sh
SRC='/path/to/game/<disc-fingerprint>/src'
dotnet build IOSRuntimeHost/IOSRuntimeHost.csproj \
  -c Release -f net10.0-ios27.0 \
  -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:CrashIosGeneratedGame=true \
  -p:CrashIosGeneratedSources="$SRC"
```

Launch on the Simulator with the host-readable CUE path:

```sh
SIMCTL_CHILD_CRASH_CUE_PATH='/path/to/Crash Bandicoot (USA).cue' \
  xcrun simctl launch <device> com.negroiso.crashlauncher.ios
```

## Current limits

- iOS cannot run the desktop/Android on-device Roslyn pipeline or dynamically
  load `game.recomp.dll`; generated sources must be compiled into the app.
- Direct VRAM/HUD writes now commit against the active display RT before
  switching targets, multiple dirty display RTs are composited in submission
  order, and VRAM presentation is selected only when those paths require it.
- iOS now defaults to the non-fetch GLES path because Apple's framebuffer-fetch
  path still misses gameplay objects on device. Set `CRASH_IOS_FB_FETCH=ext`
  only for A/B diagnostics. A fresh physical gameplay capture is still needed
  to confirm the fallback and frame rate.
- Gameplay frame rate varies between roughly 40 and 60 FPS at 1×; because the
  guest is frame-count driven, long frames currently change game speed. This
  needs profiling and pacing work.
- Audio starts and streams, but sync/pace still needs user validation after
  the full-callback fix.
- The full launcher/settings UI, touch controls, save management, mods, and a
  general distribution path are not complete.
- OpenGL ES is deprecated on iOS. This path proves the shared runtime; a Metal
  backend remains the production renderer goal.
- The simulator workload currently needs the empty `LinkSecurity` ABI stubs in
  `IOSRuntimeHost/Native` because .NET iOS 27 emits a weak framework reference
  that Xcode 27 beta no longer provides.

## Handoff checkpoint

Branch: `ios-runtime-host`; public fork:
`https://github.com/negroISO/CrashBandicoot-Launcher`.

The latest physical run reached the title, opening gameplay, the pause overlay,
and the return-to-map flow with DualSense input. Console evidence showed:

- `start=True` followed by `cross=true menu=True` and then
  `cross=true menu=false`, proving pause open/resume through GameController.
- Repeated gameplay `cross=true menu=false` events and level reloads, proving
  guest input remains live after menu resume/map return.
- AVAudioEngine started successfully after the complete-callback mixer fix.

Next diagnostic steps, in order:

1. Validate music/SFX sync and pace after the complete-callback audio fix.
2. Capture a physical gameplay frame where HUD/sprite/object content is absent
   and correlate it with direct-VRAM versus display-RT draw statistics.
3. Profile game update, GL render/present, and CAEAGLLayer present time.
   Preserve 60 virtual-vblank cadence when a rendered frame runs long instead
   of allowing guest speed to follow dips in presentation FPS.
4. Keep the 1× correctness baseline until HUD and pacing are stable; do not
   re-enable enhanced filtering/resolution while diagnosing missing content.

Local ignored evidence is under
`tmp/validation/2026-08-16-ios-runtime-host/`. Do not copy the owned disc,
generated `game/` directory, or build products into Git or the public RAG
snapshot.
