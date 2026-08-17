# Crash Bandicoot Launcher iOS port — agent rules

## Workspace

- Canonical checkout: `/Volumes/iPhone/PS1_Rrecomps/CrashBandicoot-Launcher`.
- Keep all source, builds, dependencies, logs, screenshots, and disposable artifacts on `/Volumes/iPhone`.
- Use repository-relative `tmp/` for evidence; keep build products out of Git.
- Upstream is `Matteo842/CrashBandicoot-Launcher`; the working fork remote is `negroISO/CrashBandicoot-Launcher`.

## Legal and asset boundaries

- This project is MIT-licensed tooling/runtime code only.
- Never commit a Crash Bandicoot disc dump, generated `main.cs`, `game.recomp.dll`, `game/`, retail assets, or private signing material.
- iOS public/source builds must remain user-supplied-disc workflows. Personal offline generated game code may be built locally but never pushed.

## iOS port architecture

- Preserve the portable `RecompOne.Runtime` and `RecompOne.Recompiler` boundaries.
- Keep iOS UIKit/OpenGL ES/audio code in `IOSRuntimeHost`; do not leak Apple APIs into the shared runtime.
- On-device Roslyn recompilation and dynamic `AssemblyLoadContext` loading are Android/desktop paths. iOS must use offline generation plus AOT/static linkage or a non-JIT execution strategy.
- Follow the SF1 project's validated iOS27 scene lifecycle, controller preflight, GLES-on-Metal, and external-disc security-scope patterns where applicable.

## Validation

- Every meaningful change needs the smallest relevant matrix and evidence under `tmp/validation/<date-task>/`.
- Minimum for runtime changes: macOS `net10.0` build, iOS `net10.0-ios` build, and simulator/device launch where the change is runtime-visible.
- Never claim playable status without a physical title/gameplay capture or explicit deterministic substitute.
