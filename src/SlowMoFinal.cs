using System;
using System.Diagnostics;
using GTA;
using GTA.Math;

namespace FpvDroneMod
{
    // Spec section 10. Slow-mo cinematic on impact.
    //
    // Lifecycle:
    //   1. Begin(hitPoint)               — start ramp-down 1.0 → 0.15 over 0.1s realtime
    //   2. Update() called every frame   — handles transition + post-explosion hold
    //   3. State machine ends with Done == true; caller invokes flight-exit handler.
    //
    // try/finally + abort handler in Main.cs guarantee TimeScale → 1.0 even on
    // unhandled exceptions (spec A8).
    internal sealed class SlowMoFinal
    {
        private enum Phase { RampDown, CamTransition, Hold, RampUp, Done }

        private Phase _phase = Phase.Done;
        private readonly Stopwatch _t = new Stopwatch();
        private Vector3 _hitPoint;
        private Vector3 _camStart;
        private Vector3 _camTarget;
        private bool _exploded;
        private Camera _slowMoCam;

        public bool Active => _phase != Phase.Done;
        public bool ReadyForExit => _phase == Phase.Done;
        public Vector3 HitPoint => _hitPoint;

        public void Begin(DroneState s, Camera fpvCam, Vector3 hitPoint, Ped ignorePed)
        {
            _hitPoint = hitPoint;
            _exploded = false;
            _camStart = fpvCam.Position;
            _camTarget = ChooseCinematicCameraPosition(s, hitPoint, ignorePed);

#pragma warning disable CS0618 // see Main.cs note
            _slowMoCam = World.CreateCamera(_camStart, fpvCam.Rotation, fpvCam.FieldOfView);
            World.RenderingCamera = _slowMoCam;
#pragma warning restore CS0618

            _phase = Phase.RampDown;
            _t.Restart();
        }

        // Per-frame realtime update.
        public void Update(DroneState s)
        {
            float t = (float)_t.Elapsed.TotalSeconds;

            switch (_phase)
            {
                case Phase.RampDown:
                {
                    // 1.0 → 0.15 over 0.1s realtime
                    float k = Math.Min(1f, t / Config.TimeScaleRamp);
                    Game.TimeScale = 1.0f + (Config.TimeScaleSlow - 1.0f) * k;

                    if (k >= 1f)
                    {
                        _phase = Phase.CamTransition;
                        _t.Restart();
                    }
                    break;
                }

                case Phase.CamTransition:
                {
                    float k = Math.Min(1f, t / Config.CamTransition);
                    Vector3 pos = _camStart + (_camTarget - _camStart) * k;
                    if (_slowMoCam != null && _slowMoCam.Exists())
                    {
                        _slowMoCam.Position = pos;
                        _slowMoCam.PointAt(_hitPoint);
                    }
                    if (k >= 1f)
                    {
                        // Detonate at the moment the camera reaches its anchor.
                        if (!_exploded)
                        {
                            Collision.Detonate(_hitPoint, s.V.Length());
                            _exploded = true;
                        }
                        _phase = Phase.Hold;
                        _t.Restart();
                    }
                    break;
                }

                case Phase.Hold:
                {
                    if (t >= Config.CamHoldAfterExplosion)
                    {
                        _phase = Phase.RampUp;
                        _t.Restart();
                    }
                    break;
                }

                case Phase.RampUp:
                {
                    float k = Math.Min(1f, t / Config.TimeScaleRamp);
                    Game.TimeScale = Config.TimeScaleSlow + (1.0f - Config.TimeScaleSlow) * k;
                    if (k >= 1f)
                    {
                        Game.TimeScale = 1.0f;
                        Cleanup();
                        _phase = Phase.Done;
                    }
                    break;
                }
            }
        }

        public void ForceAbort()
        {
            // Called from OnAborted + emergency exit. Always safe to call.
            try { Game.TimeScale = 1.0f; } catch { }
            Cleanup();
            _phase = Phase.Done;
            _exploded = false;
        }

        private void Cleanup()
        {
            if (_slowMoCam != null && _slowMoCam.Exists())
            {
                try { _slowMoCam.Delete(); } catch { }
            }
            _slowMoCam = null;
        }

        // Spec 10.3 — find a viable cinematic camera position.
        // Tries hit_point + (-F) * D_cam + (0, 0, H_cam). If the path collides,
        // pulls back to 80% of the unobstructed distance. If even that's too
        // tight, falls back to drone position so we don't stick the camera
        // inside a wall.
        private static Vector3 ChooseCinematicCameraPosition(DroneState s, Vector3 hitPoint, Ped ignorePed)
        {
            Vector3 back = -s.F;
            Vector3 candidate = hitPoint + back * Config.DCam + new Vector3(0, 0, Config.HCam);

            var checkRay = World.Raycast(hitPoint, candidate, IntersectFlags.Map, ignorePed);
            if (!checkRay.DidHit)
                return candidate;

            float clear = (checkRay.HitPosition - hitPoint).Length();
            if (clear < Config.CamMinClearance)
                return s.P; // too tight — keep FPV-ish view

            Vector3 dir = candidate - hitPoint;
            float fullLen = dir.Length();
            if (fullLen < 0.01f) return candidate;
            return hitPoint + (dir / fullLen) * (clear * 0.8f);
        }
    }
}
