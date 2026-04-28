using System;
using System.Diagnostics;
using GTA;
using GTA.Math;

namespace FpvDroneMod
{
    // Spec section 10 (rewritten for "camera = warhead, contact-only" model).
    //
    // Sequence triggered the instant the drone's contact-frame raycast hits
    // a surface. There is no predictive slow-down before contact.
    //
    //   Begin(hitPoint)                   — t = 0 at moment of impact
    //   ├── Phase.RampDown   (0 → 0.1s)   TimeScale 1.0 → 0.15
    //   ├── Phase.CamMove    (0.1 → 0.18) FPV cam interpolates back to a
    //   │                                 raycast-clear cinematic anchor
    //   │                                 pointing at hit_point. Explosion
    //   │                                 (ADD_EXPLOSION) fires once at end
    //   │                                 of this lerp.
    //   ├── Phase.Hold       (0.18 → 2.18) cinematic camera holds
    //   ├── Phase.RampUp     (2.18 → 2.28) TimeScale 0.15 → 1.0
    //   └── Phase.Done                    Main calls EndFlight()
    //
    // try/finally + abort handler in Main.cs guarantee TimeScale → 1.0 even
    // on unhandled exceptions (spec A8).
    internal sealed class SlowMoFinal
    {
        private enum Phase { Done, RampDown, CamMove, Hold, RampUp }

        private Phase _phase = Phase.Done;
        private readonly Stopwatch _t = new Stopwatch();
        private Vector3 _hitPoint;
        private Vector3 _approachDir;   // unit vector along which the drone hit
        private Vector3 _camStart;
        private Vector3 _camTarget;
        private bool _exploded;
        private float _impactSpeed;     // captured at Begin so SLow-mo can use
                                        //   the *real* impact speed even after
                                        //   we zero V to halt the drone.
        private Camera _slowMoCam;

        public bool Active => _phase != Phase.Done;
        public bool ReadyForExit => _phase == Phase.Done && _exploded;

        // Called from Main on the contact frame, immediately after the drone
        // is snapped to hit_point − F*SnapInset. Spawns the cinematic camera
        // co-located with the FPV camera so the cut is invisible, then begins
        // ramping time scale + interpolating the cinematic anchor.
        public void Begin(DroneState s, Camera fpvCam, Vector3 hitPoint,
                          Vector3 approachDir, float impactSpeed, Ped ignorePed)
        {
            _hitPoint = hitPoint;
            _approachDir = approachDir;
            _impactSpeed = impactSpeed;
            _exploded = false;
            _camStart = fpvCam.Position;
            _camTarget = ChooseCinematicCameraPosition(s, hitPoint, ignorePed);

#pragma warning disable CS0618 // see Main.cs note on RenderingCamera
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
                    // 1.0 → 0.15 over TimeScaleRamp s realtime.
                    float k = Math.Min(1f, t / Config.TimeScaleRamp);
                    Game.TimeScale = 1.0f + (Config.TimeScaleSlow - 1.0f) * k;
                    if (k >= 1f)
                    {
                        _phase = Phase.CamMove;
                        _t.Restart();
                    }
                    break;
                }

                case Phase.CamMove:
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
                        if (!_exploded)
                        {
                            Collision.Detonate(_hitPoint, _approachDir, _impactSpeed);
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
        // Tries hit_point + (-F) * D_cam + (0, 0, H_cam). If the path collides
        // (e.g. the drone hit a ceiling and the back-and-up offset would put
        // the camera in the wall behind the drone), pull back to 80% of the
        // unobstructed distance. If that's still too tight, fall back to a
        // small offset above the impact point so we never end up inside a
        // surface.
        private static Vector3 ChooseCinematicCameraPosition(DroneState s, Vector3 hitPoint, Ped ignorePed)
        {
            Vector3 back = -s.F;
            Vector3 candidate = hitPoint + back * Config.DCam + new Vector3(0, 0, Config.HCam);

            var checkRay = World.Raycast(hitPoint, candidate, IntersectFlags.Map, ignorePed);
            if (!checkRay.DidHit)
                return candidate;

            float clear = (checkRay.HitPosition - hitPoint).Length();
            if (clear < Config.CamMinClearance)
            {
                // Too tight — drop back to just above the drone position.
                return s.P + new Vector3(0, 0, 1.5f);
            }
            // Pull back to 80% of the clear distance along the same ray direction.
            return hitPoint + (checkRay.HitPosition - hitPoint) * 0.8f;
        }
    }
}
