FPV Kamikaze Drone — release build
===================================

Built against ScriptHookVDotNet v3.7.0-nightly.81 (sha ca029d4).
Targets .NET Framework 4.8.

Installation
------------
1. Make sure ScriptHookV + ScriptHookVDotNet (nightly v3.7.0-nightly.81 or
   compatible) are already installed in your GTA V folder.
2. Copy FpvDroneMod.dll into:   <GTA V>\scripts\FpvDroneMod.dll
3. (Optional) Copy FpvDroneMod.pdb alongside it for cleaner SHVDN crash logs.
4. Launch the game in single-player. The script auto-loads.

Controls
--------
G          launch / exit drone
H          emergency exit
Mouse      yaw + pitch
T / Y      throttle +2 / -2 m/s
PgUp/PgDn  vertical +8 / -8 m/s

Notes
-----
- Audio is intentionally not implemented in this build.
- Multiplayer is auto-blocked.
- Cannot launch from a vehicle / cutscene / under a roof / while swimming etc.
- Player remains visible (alpha 100) but invincible + collision off; cops and
  NPCs can see and shoot you, but damage doesn't apply.
- Wanted multiplier is forced to 0 during flight; rolled back on exit.
