# FPV Kamikaze Drone — GTA 5 SP Mod

A first-person FPV kamikaze drone simulator for GTA 5 single-player. The drone
is a virtual point in space (no physics body): camera attached as FPV goggles,
mouse + keyboard control, finite battery, radio-signal range with interference
degradation, slow-motion cinematic on impact, grenade-type explosion.

Implements the v2.0 spec including all safety guards from the audit:
- Player teleported to drone position every frame, but with collision off /
  invincible / alpha 100 (visible ghost).
- All raycasts ignore the player ped (so the drone doesn't self-hit).
- `ADD_EXPLOSION` without an owner — no wanted level for the drone strike.
- All three control groups blocked, mouse read via `GET_DISABLED_CONTROL_NORMAL`.
- `try/finally` around `SET_TIME_SCALE` and aborted-handler reset.
- All slow-mo / lost-signal / recovery timers measured in realtime
  (`Stopwatch`), separated from physics `dt_game`.
- Explicit `× 180/π` conversion for `Camera.Rotation` (degrees).

## Install (precompiled)

A prebuilt DLL for **ScriptHookVDotNet v3.7.0-nightly.81** lives in
[`release/FpvDroneMod.dll`](release/FpvDroneMod.dll). Drop it into your GTA V
`scripts/` folder and launch.

## Build from source

Requires **.NET Framework 4.8** target. Either:

- **Windows / Visual Studio 2022:** open the `.csproj` and build.
- **Cross-platform via .NET SDK 8 + reference assemblies:**
  ```
  dotnet build FpvDroneMod.csproj -c Release
  ```
  The csproj uses `Microsoft.NETFramework.ReferenceAssemblies` so it compiles on
  Linux/macOS without a Windows SDK.

The output `FpvDroneMod.dll` goes into your GTA V `scripts/` folder alongside
`ScriptHookVDotNet3.asi` / `ScriptHookVDotNet3.dll`.

`libs/ScriptHookVDotNet3.dll` and `.xml` are bundled for build-time reference
only (currently v3.7.0-nightly.81). They are NOT copied to the output
(`Private=False`).

## Controls

| Action | Key |
|---|---|
| Launch / exit | **G** |
| Emergency exit | **H** |
| Yaw / pitch | Mouse |
| Throttle + / − | **T** / **Y** |
| Vertical + / − | **PgUp** / **PgDn** |

## Limitations

- Single-player only (auto-disables in MP).
- Audio is intentionally not implemented in this revision (per request).
- Custom shader effects (true chromatic aberration, scanlines) are
  approximated via stock `ANIMPOSTFX` presets + `UIRectangle` overlays —
  there is no shader hook in ScriptHookVDotNet.
- Cops / NPCs can see the visible ghost player and react/shoot, but invincible
  + collision-off means damage doesn't apply. `WANTED_LEVEL_MULTIPLIER = 0`
  prevents new wanted while flying. This is intentional — adds atmosphere of
  flying under fire.
