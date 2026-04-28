using System;
using System.Diagnostics;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

namespace FpvDroneMod
{
    // Top-level orchestrator — implements spec section 13 frame loop.
    //
    // Hot-path responsibilities:
    //   1. Detect launch / abort / pause via KeyDown + IsPauseMenuActive.
    //   2. Run the pre-flight check (PlayerGuard).
    //   3. Each Tick while flying:
    //      - block all controls
    //      - read disabled controls for mouse + keyboard intent
    //      - update battery, signal, stage
    //      - apply input distortion
    //      - integrate physics
    //      - run a forward raycast (Collision)
    //      - drive slow-mo state machine if impact imminent
    //      - update camera + teleport player ped
    //      - draw HUD + interference overlay
    //   4. On exit: hand off to PlayerGuard.OnExit + restore camera.
    public sealed class Main : Script
    {
        private DroneState _state;
        private Camera _fpvCam;
        private bool _flying;
        private bool _mouseFirstFrame = true;

        // Realtime stopwatch — used for slow-mo, lost timer, recovery, etc. so
        // we don't depend on Game.LastFrameTime which is in game-time and gets
        // multiplied by TimeScale during slow-mo.
        private readonly Stopwatch _rt = new Stopwatch();
        private TimeSpan _rtPrev;

        private SlowMoFinal _slowMo = new SlowMoFinal();

        public Main()
        {
            Tick    += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
            Interval = 0;
            _rt.Start();
        }

        // ---- Lifecycle ----

        private void TryStartFlight()
        {
            var player = Game.Player;
            var ped = player.Character;
            string err = PlayerGuard.PreflightCheck(player, ped);
            if (err != null)
            {
                Notification.PostTicker("FPV: " + err, false, false);
                return;
            }

            _state = new DroneState();
            PlayerGuard.OnEnter(player, ped, _state);
            InputDistortion.Reset();

#pragma warning disable CS0618 // World.CreateCamera + RenderingCamera deprecated in v3.7 nightly but still functional
            // Spawn camera looking the same way the player was — so the first
            // frame doesn't snap-yaw to north.
            _fpvCam = World.CreateCamera(_state.P,
                new Vector3(_state.Theta * MathF.Rad2Deg, 0f, _state.Psi * MathF.Rad2Deg),
                Config.FpvFov);
            World.RenderingCamera = _fpvCam;
#pragma warning restore CS0618

            // Seed s.F from the live camera so frame 0 physics aren't garbage.
            _state.F = _fpvCam.ForwardVector;

            _mouseFirstFrame = true;
            MouseCapture.Recenter();
            _flying = true;
        }

        private void EndFlight()
        {
            if (!_flying) return;

            try { _slowMo.ForceAbort(); } catch { }
            Effects.StopAll();

#pragma warning disable CS0618
            World.RenderingCamera = null;
#pragma warning restore CS0618
            if (_fpvCam != null && _fpvCam.Exists())
            {
                try { _fpvCam.Delete(); } catch { }
            }
            _fpvCam = null;

            try { PlayerGuard.OnExit(Game.Player, Game.Player.Character, _state); } catch { }

            _flying = false;
        }

        // ---- Events ----

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.G)
            {
                if (_flying) EndFlight();
                else TryStartFlight();
                return;
            }

            if (!_flying) return;

            switch (e.KeyCode)
            {
                case Keys.H:
                    EndFlight();
                    break;
                case Keys.T:
                    Physics.AdjustThrottle(_state, +Config.DTStep);
                    break;
                case Keys.Y:
                    Physics.AdjustThrottle(_state, -Config.DTStep);
                    break;
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            // Critical: even on abort, restore world state.
            try { Game.TimeScale = 1.0f; } catch { }
            try { _slowMo.ForceAbort(); } catch { }
            try { Effects.StopAll(); } catch { }
            try { EndFlight(); } catch { }
        }

        // ---- Per-frame loop (spec section 13) ----

        private void OnTick(object sender, EventArgs e)
        {
            // Realtime delta for timers that must not depend on TimeScale.
            TimeSpan now = _rt.Elapsed;
            float dtReal = (float)(now - _rtPrev).TotalSeconds;
            if (dtReal < 0f) dtReal = 0f;
            if (dtReal > Config.DtClampMax) dtReal = Config.DtClampMax;
            _rtPrev = now;

            if (!_flying) return;

            // Slow-mo end-game state machine takes priority — once we're in it,
            // physics + collision + input are frozen.
            if (_slowMo.Active)
            {
                _slowMo.Update(_state);
                Hud.Draw(_state, Config.FpvFov);
                Effects.DrawOverlays(Math.Min(1.0f, _state.I + 0.4f)); // crank during impact
                if (_slowMo.ReadyForExit) EndFlight();
                return;
            }

            // Pause menu — freeze simulation, keep camera, skip input.
            if (Natives.IsPauseMenuActive() || Game.IsPaused)
            {
                Hud.Draw(_state, Config.FpvFov);
                return;
            }

            float dtGame = Game.LastFrameTime;
            if (dtGame > Config.DtClampMax) dtGame = Config.DtClampMax;

            var player = Game.Player;
            var ped = player.Character;

            // 13.3 Block all 3 control groups.
            Natives.DisableAllControlsThisFrame();

            // 13.5 Read mouse via Win32 raw cursor delta. The disabled-control
            // path returns 0 here because the gameplay camera is replaced by a
            // scripted camera and the engine stops feeding LookLeftRight/UpDown.
            var (mdx, mdy) = MouseCapture.ReadDelta(ref _mouseFirstFrame);
            float dmxRaw = mdx / Config.MousePixelsPerUnit;
            float dmyRaw = mdy / Config.MousePixelsPerUnit;

            // 13.7 Vertical input — read PgUp/PgDn directly via Win32 (works
            // independently of the GTA control system).
            float vVert = 0f;
            if ((NativeKey.GetAsyncKeyState((int)Keys.PageUp) & 0x8000) != 0) vVert += Config.VVertStep;
            if ((NativeKey.GetAsyncKeyState((int)Keys.PageDown) & 0x8000) != 0) vVert -= Config.VVertStep;

            // 13.8 Battery
            Physics.DrainBattery(_state, dtGame);
            bool batteryDead = _state.B <= 0.0001f;
            if (batteryDead && _state.MotorsDeadBannerT <= 0f) _state.MotorsDeadBannerT = 0.5f;
            _state.MotorsDeadBannerT = Math.Max(0f, _state.MotorsDeadBannerT - dtReal);
            _state.SignalRestoredBannerT = Math.Max(0f, _state.SignalRestoredBannerT - dtReal);

            // 13.1-2 LoS refresh on cadence.
            _state.LosCheckTimer += dtReal;
            if (_state.LosCheckTimer >= Config.LosCheckInterval)
            {
                Signal.RefreshLineOfSight(_state, ped);
                _state.LosCheckTimer = 0f;
            }

            // 13.10-15 Q / I.
            Signal.ComputeQuality(_state);

            // 13.16-17 Stage transitions.
            Signal.UpdateStage(_state, dtReal);

            // 13.18-19 Distortion / autonomous mode.
            (float dmx, float dmy) = (dmxRaw, dmyRaw);
            (dmx, dmy) = InputDistortion.ApplyDelay(dmx, dmy, _state.I, _state.Stage, dtReal);
            (dmx, dmy) = InputDistortion.Apply(dmx, dmy, _state.I, _state.Stage);
            _state.LastDmx = dmx; _state.LastDmy = dmy;

            // 13.20-23 Angular update + roll + forward vector.
            Physics.ApplyAngularInput(_state, dmx, dmy, dtGame);
            Physics.UpdateCameraRoll(_state, dmx, dtGame);

            // Apply the new orientation to the camera FIRST, then read its
            // engine-canonical forward vector. This guarantees physics moves
            // the drone in the exact direction the camera is looking — no
            // matter which Euler-angle convention SHVDN/GTA actually use.
            if (_fpvCam != null && _fpvCam.Exists())
            {
                _fpvCam.Rotation = new Vector3(
                    _state.Theta  * MathF.Rad2Deg,
                    _state.PhiCam * MathF.Rad2Deg,
                    _state.Psi    * MathF.Rad2Deg);
                _state.F = _fpvCam.ForwardVector;
            }

            // 13.24-30 Velocity / position integration. Battery dead → motors
            // off (T=0), drag mode = dead.
            if (batteryDead) _state.T = 0f;
            Physics.IntegrateMotion(_state, vVert, dtGame, batteryDead);

            // 13.31-33 Forward raycast.
            var (outcome, hit, speed) = Collision.Step(_state, dtGame, ped);
            if (outcome == Collision.Outcome.ImpactNow)
            {
                Collision.Detonate(hit, speed);
                EndFlight();
                return;
            }
            else if (outcome == Collision.Outcome.ImpactImminent)
            {
                _state.ImpactImminent = true;
                _slowMo.Begin(_state, _fpvCam, hit, ped);
                return; // next tick will run slow-mo update
            }

            // 13.34 Emergency boundary check.
            float d = (_state.P - _state.Spawn).Length();
            float dMax = _state.LoS ? Config.DMaxField : Config.DMaxCity;
            if (d > dMax + Config.EmergencyOverDMax)
            {
                Collision.Detonate(_state.P, speed);
                EndFlight();
                return;
            }

            // 13.36 Teleport player ped to drone position (no offset).
            Natives.SetEntityCoordsNoOffset(ped, _state.P);
            Natives.SetEntityHeading(ped, _state.Psi * MathF.Rad2Deg);
            Natives.SetRadarAsExteriorThisFrame();

            // 13.37 Camera position update — rotation was already applied
            // above so we could read the canonical forward vector.
            if (_fpvCam != null && _fpvCam.Exists())
                _fpvCam.Position = _state.P;

            // 13.38 Drone whine — intentionally skipped (no audio in this rev).

            // 13.39 Interference effects.
            Effects.Update(_state.I);
            Effects.DrawOverlays(_state.I);

            // 13.40 HUD.
            Hud.Draw(_state, Config.FpvFov);
        }
    }

    // Direct Win32 keystate access for keys that don't have stable Control
    // mappings (e.g. PageUp/PageDown). SHVDN's KeyDown event handles momentary
    // presses, but for held-down vertical movement we want polled state.
    internal static class NativeKey
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
    }

    // Raw mouse-delta capture. We can't use GET_DISABLED_CONTROL_NORMAL for
    // LookLeftRight/LookUpDown because the gameplay camera is replaced by a
    // scripted camera (World.RenderingCamera) and the engine stops feeding
    // those controls. Instead we sample the OS cursor every frame and snap it
    // back to screen centre so we always read a true delta.
    internal static class MouseCapture
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct Win32Point { public int X; public int Y; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Win32Point lpPoint);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        public static int CenterX => System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width  / 2;
        public static int CenterY => System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height / 2;

        // Returns (dx, dy) in pixels since last call, then snaps cursor back
        // to screen centre. The first call after Reset() returns (0, 0) so the
        // initial cursor position doesn't get interpreted as a giant flick.
        public static (int dx, int dy) ReadDelta(ref bool firstFrame)
        {
            int cx = CenterX;
            int cy = CenterY;
            Win32Point p;
            GetCursorPos(out p);
            int dx = p.X - cx;
            int dy = p.Y - cy;
            SetCursorPos(cx, cy);
            if (firstFrame) { firstFrame = false; dx = 0; dy = 0; }
            return (dx, dy);
        }

        public static void Recenter() { SetCursorPos(CenterX, CenterY); }
    }
}
