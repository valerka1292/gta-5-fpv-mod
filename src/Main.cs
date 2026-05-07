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
            Log.Init();
            AudioManager.Init();
            Log.Info("FPV Drone Mod starting up");
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
            Natives.SetSeethrough(false);
            Natives.SetNightvision(false);

#pragma warning disable CS0618 // World.CreateCamera + RenderingCamera deprecated in v3.7 nightly but still functional
            // Spawn camera looking the same way the player was — so the first
            // frame doesn't snap-yaw to north.
            _fpvCam = World.CreateCamera(_state.P,
                new Vector3(_state.Theta * MathF.Rad2Deg, 0f, _state.Psi * MathF.Rad2Deg),
                Config.FpvFov);
            World.RenderingCamera = _fpvCam;
#pragma warning restore CS0618

            // Seed s.F from the analytical formula. We CANNOT call
            // _fpvCam.ForwardVector here — the engine hasn't registered the
            // script camera until the next render frame, and SHVDN's
            // NativeMemory.GetCameraAddress() returns null → NRE.
            _state.F = Physics.ForwardFromYawPitch(_state.Psi, _state.Theta);

            _mouseFirstFrame = true;
            MouseCapture.Recenter();
            _flying = true;
            _state.Spooling     = true;
            _state.SpoolElapsed = 0f;
            AudioManager.OnLaunch();
        }

        private void EndFlight()
        {
            if (!_flying) return;
            AudioManager.OnStop();

            try { _slowMo.ForceAbort(); }
            catch (Exception ex) { Log.Error("EndFlight: SlowMo.ForceAbort failed", ex); }

            try { Effects.StopAll(); }
            catch (Exception ex) { Log.Error("EndFlight: Effects.StopAll failed", ex); }

            try
            {
                Natives.SetSeethrough(false);
                Natives.SetNightvision(false);
            }
            catch (Exception ex) { Log.Error("EndFlight: Resetting vision modes failed", ex); }

            try
            {
#pragma warning disable CS0618
                World.RenderingCamera = null;
#pragma warning restore CS0618
            }
            catch (Exception ex) { Log.Error("EndFlight: clearing RenderingCamera failed", ex); }

            if (_fpvCam != null && _fpvCam.Exists())
            {
                try { _fpvCam.Delete(); }
                catch (Exception ex) { Log.Error("EndFlight: fpvCam.Delete failed", ex); }
            }
            _fpvCam = null;

            // Final defence: Game.TimeScale must always come back to 1.0 so
            // the player isn't permanently stuck in slow motion.
            try { Game.TimeScale = 1.0f; }
            catch (Exception ex) { Log.Error("EndFlight: TimeScale reset failed", ex); }

            try { PlayerGuard.OnExit(Game.Player, Game.Player.Character, _state); }
            catch (Exception ex) { Log.Error("EndFlight: PlayerGuard.OnExit failed", ex); }

            _flying = false;
        }

        // ---- Events ----

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Settings menu absorbs all input while open. Only allowed to open
            // from non-flying state — opening it mid-flight would conflict
            // with the mouse-capture loop.
            if (Menu.IsOpen)
            {
                Menu.OnKey(e.KeyCode);
                return;
            }
            if (e.KeyCode == Keys.F9)
            {
                if (!_flying) Menu.Toggle();
                return;
            }

            if (e.KeyCode == Keys.G)
            {
                if (_flying) EndFlight();
                else TryStartFlight();
                return;
            }

            if (!_flying) return;

            if (e.KeyCode == Keys.J)
            {
                // Cycle through visual modes: Normal -> Thermal -> Night Vision -> Normal.
                _state.Vision = (VisionMode)(((int)_state.Vision + 1) % 3);

                if (_state.Vision == VisionMode.Normal)
                {
                    Natives.SetSeethrough(false);
                    Natives.SetNightvision(false);
                }
                else if (_state.Vision == VisionMode.Thermal)
                {
                    Natives.SetNightvision(false);
                    Natives.SetSeethrough(true);
                }
                else if (_state.Vision == VisionMode.NightVision)
                {
                    Natives.SetSeethrough(false);
                    Natives.SetNightvision(true);
                }
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.H:
                    EndFlight();
                    break;
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            // Critical: even on abort, restore world state.
            Log.Warn("Main.OnAborted called — restoring world state");
            try { Game.TimeScale = 1.0f; }
            catch (Exception ex) { Log.Error("Aborted: TimeScale reset failed", ex); }
            try { _slowMo.ForceAbort(); }
            catch (Exception ex) { Log.Error("Aborted: SlowMo.ForceAbort failed", ex); }
            try { Effects.StopAll(); }
            catch (Exception ex) { Log.Error("Aborted: Effects.StopAll failed", ex); }
            try { AudioManager.OnStop(); }
            catch (Exception ex) { Log.Error("Aborted: AudioManager.OnStop failed", ex); }
            try { EndFlight(); }
            catch (Exception ex) { Log.Error("Aborted: EndFlight failed", ex); }
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

            // Settings menu can be drawn whether we're flying or not. When
            // open it absorbs all input via OnKeyDown.
            if (Menu.IsOpen)
            {
                // Блокируем все игровые действия, чтобы стрелочки не клацали телефон
                Natives.DisableAllControlsThisFrame();
                Menu.Draw();
                if (!_flying) return;
            }

            if (!_flying) return;

            // Slow-mo state machine takes priority once started — physics +
            // input are frozen for the cinematic. Slow-mo is now triggered
            // only by an actual contact-frame collision (no predictive
            // slow-down before impact); see Collision.Step.
            if (_slowMo.Active)
            {
                _slowMo.Update(_state);
                Hud.Draw(_state, Config.FpvFov);
                Effects.DrawOverlays(Math.Min(1.0f, _state.I + 0.4f), _state.WallsCount);
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
            if ((NativeKey.GetAsyncKeyState((int)Keys.T) & 0x8000) != 0)
                Physics.AdjustThrottle(_state, +Config.ThrottleRate * dtGame);
            if ((NativeKey.GetAsyncKeyState((int)Keys.Y) & 0x8000) != 0)
                Physics.AdjustThrottle(_state, -Config.ThrottleRate * dtGame);

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
            Physics.ApplyAngularInput(_state, dmx, dmy, dtGame, _state.Stage);
            Physics.UpdateCameraRoll(_state, dmx, dtGame);

            // Apply the new orientation to the camera FIRST, then read its
            // engine-canonical forward vector. This guarantees physics moves
            // the drone in the exact direction the camera is looking — no
            // matter which Euler-angle convention SHVDN/GTA actually use.
            // Reading ForwardVector can NRE on the very first frame (engine
            // hasn't registered the cam yet) — fall back to analytical.
            if (_fpvCam != null && _fpvCam.Exists())
            {
                _fpvCam.Rotation = new Vector3(
                    _state.Theta  * MathF.Rad2Deg,
                    _state.PhiCam * MathF.Rad2Deg,
                    _state.Psi    * MathF.Rad2Deg);
                try
                {
                    Vector3 fwd = _fpvCam.ForwardVector;
                    if (fwd.LengthSquared() > 0.5f) _state.F = fwd;
                    else _state.F = Physics.ForwardFromYawPitch(_state.Psi, _state.Theta);
                }
                catch
                {
                    _state.F = Physics.ForwardFromYawPitch(_state.Psi, _state.Theta);
                }
            }
            else
            {
                _state.F = Physics.ForwardFromYawPitch(_state.Psi, _state.Theta);
            }

            // 13.24-30 Velocity / position integration. Battery dead → motors
            // off (T=0), drag mode = dead.
            // Раскрутка при старте: плавный разгон от 0 до TSpoolUp за TSpoolDuration секунд
            if (_state.Spooling)
            {
                _state.SpoolElapsed += dtReal;
                float spoolT = Physics.Clamp01(_state.SpoolElapsed / Config.TSpoolDuration);
                // Квадратичная кривая для плавного нарастания
                _state.T = spoolT * spoolT * Math.Min(Config.TSpoolUp, global::FpvDroneMod.Settings.CurrentProfile.TMax);
                if (_state.SpoolElapsed >= Config.TSpoolDuration)
                {
                    _state.Spooling    = false;
                    _state.T           = Math.Min(Config.TSpoolUp, global::FpvDroneMod.Settings.CurrentProfile.TMax);
                }
            }
            if (batteryDead) _state.T = 0f;

            // Capture pre-integration position so the swept collision ray
            // covers the actual frame's travel — fixes high-speed tunneling
            // through cars/peds.
            Vector3 prevP = _state.P;
            // Симуляция OSD-телеметрии: время полёта и расход ёмкости
            _state.FlightTimerReal += dtReal;
            float currentAmps = (_state.T / global::FpvDroneMod.Settings.CurrentProfile.TMax) * 35f;   // ток зависит от газа
            _state.MahUsed += currentAmps * dtReal / 3.6f;        // А·с → мАч
            Physics.IntegrateMotion(_state, vVert, dtGame, batteryDead);

            // NPC reaction: panic, wanted escalation, cop suppressive fire.
            NpcReaction.Update(_state, ped, player, dtReal);

            // 13.31-33 Swept-volume contact ray (prevP → curP + margin).
            var (outcome, hit, hitNormal, hitEntity, speed) = Collision.Step(_state, prevP, dtGame, ped);
            if (outcome == Collision.Outcome.ImpactNow)
            {
                // Snap the drone (and therefore the FPV camera) to just
                // shy of the surface along the actual approach direction
                // (prevP → hit), not along F — at high speed inertia can
                // make trajectory diverge from where the camera was
                // looking. We back off by SnapInset metres so the camera
                // kisses the texture without poking through.
                Vector3 approach = hit - prevP;
                Vector3 approachDir = approach.LengthSquared() > 0.0001f
                    ? Vector3.Normalize(approach)
                    : _state.F;
                // Capture impact speed BEFORE we zero out V so the explosion
                // and vehicle-impulse can scale by how hard we actually hit.
                float impactSpeed = _state.V.Length();
                Vector3 impactVelocity = _state.V;

                // Capture pre-snap camera position so SlowMoFinal can start
                // its cinematic interpolation from where the player was
                // *actually* looking the frame before impact, instead of
                // from the snapped-to-wall position. Without this the
                // first slow-mo frame shows the texture in the player's
                // face and then the camera "rubber-bands" backward — felt
                // like a glitch.
                Vector3 fpvCamPrePos = (_fpvCam != null && _fpvCam.Exists())
                    ? _fpvCam.Position
                    : _state.P;

                _state.P = hit - approachDir * Config.SnapInset;
                _state.V = Vector3.Zero;
                if (_fpvCam != null && _fpvCam.Exists())
                    _fpvCam.Position = _state.P;

                _state.ImpactImminent = true;
                Collision.ApplyKineticImpactImpulse(hitEntity, hit, hitNormal, impactVelocity);
                AudioManager.OnCrash();
                _slowMo.Begin(_state, _fpvCam, hit, approachDir, impactSpeed,
                              fpvCamPrePos, ped);
                return;
            }

            // 13.34 Emergency boundary check.
            float d = (_state.P - _state.Spawn).Length();
            float dMax = _state.LoS ? Config.DMaxField : Config.DMaxCity;
            if (d > dMax + Config.EmergencyOverDMax)
            {
                // Emergency detonation past D_max — no real impact, so use
                // the drone's own velocity direction as approachDir fallback.
                Vector3 emerDir = _state.V.LengthSquared() > 0.001f
                    ? Vector3.Normalize(_state.V)
                    : _state.F;
                Collision.Detonate(_state.P, emerDir, speed);
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

            // 13.38 Аудио-движок дрона
            AudioManager.Update(_state);

            // 13.39 Interference effects.
            Effects.Update(_state.I);
            Effects.DrawOverlays(_state.I, _state.WallsCount);

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
