using System;
using System.Diagnostics;
using GTA;
using GTA.Math;

namespace FpvDroneMod
{
    // "Camera = warhead" model.
    //
    // Predictive collision detects impact a few metres ahead. We then ramp the
    // time scale down to slow-mo, but DO NOT swap to a cinematic camera or
    // detonate yet. The FPV camera keeps tracking the drone as it slowly
    // glides forward toward the hit point — the player gets the visceral
    // "ramming the wall" feeling. Only when the drone actually arrives
    // (within DetonationProximity m, or after a hard timeout) do we:
    //
    //   1. spawn the cinematic camera at a raycast-clear point behind/above
    //      the impact and point it at the hit point,
    //   2. fire ADD_EXPLOSION,
    //   3. hold the cinematic camera ~2 s realtime,
    //   4. ramp time scale back up,
    //   5. flag ReadyForExit so Main can clean up.
    //
    // try/finally + abort handler in Main.cs guarantee TimeScale → 1.0 even on
    // unhandled exceptions (spec A8).
    internal sealed class SlowMoFinal
    {
        private enum Phase { Inactive, Approach, Hold, RampUp }

        private Phase _phase = Phase.Inactive;
        private readonly Stopwatch _t = new Stopwatch();
        private Vector3 _hitPoint;
        private bool _exploded;
        private Camera _slowMoCam;

        public bool Active => _phase != Phase.Inactive;
        public bool ReadyForExit => _phase == Phase.Inactive && _exploded;
        // While true, Main keeps running normal physics (time scale is already
        // ramped down so the drone advances slowly).
        public bool AllowsPhysics => _phase == Phase.Approach;
        public Vector3 HitPoint => _hitPoint;

        // Stage 1 — predictive ray fired, slow-mo ramp-down begins. Drone keeps
        // flying under normal physics; the world is just slow.
        public void BeginApproach(Vector3 hitPoint)
        {
            _hitPoint = hitPoint;
            _exploded = false;
            _phase = Phase.Approach;
            _t.Restart();
        }

        // Called every frame from Main during Approach phase to decide whether
        // the drone has actually reached the wall.
        public bool ShouldDetonate(DroneState s)
        {
            if (_phase != Phase.Approach) return false;

            float distToHit = (s.P - _hitPoint).Length();
            if (distToHit < Config.DetonationProximity) return true;

            // Hard timeout: if user steered enough that the drone never quite
            // reaches hit_point, complete the mission anyway after a few
            // realtime seconds.
            if (_t.Elapsed.TotalSeconds > Config.MaxApproachSeconds) return true;

            return false;
        }

        // Stage 2 — drone has touched. Spawn cinematic cam, ADD_EXPLOSION,
        // proceed into Hold/RampUp.
        public void TriggerDetonate(DroneState s, Camera fpvCam, Ped ignorePed)
        {
            if (_exploded) return;

            // Use the drone's current position as the explosion centre — the
            // player just rammed it, so the warhead really is here.
            Vector3 explosionAt = s.P;

            Vector3 camPos = ChooseCinematicCameraPosition(s, explosionAt, ignorePed);

#pragma warning disable CS0618 // see Main.cs note
            _slowMoCam = World.CreateCamera(camPos, fpvCam.Rotation, fpvCam.FieldOfView);
            _slowMoCam.PointAt(explosionAt);
            World.RenderingCamera = _slowMoCam;
#pragma warning restore CS0618

            Collision.Detonate(explosionAt, s.V.Length());
            _exploded = true;

            _phase = Phase.Hold;
            _t.Restart();
        }

        // Per-frame realtime update — handles time-scale envelope.
        public void Update(DroneState s)
        {
            float t = (float)_t.Elapsed.TotalSeconds;

            switch (_phase)
            {
                case Phase.Approach:
                {
                    // 1.0 → 0.15 over TimeScaleRamp s realtime.
                    float k = Math.Min(1f, t / Config.TimeScaleRamp);
                    Game.TimeScale = 1.0f + (Config.TimeScaleSlow - 1.0f) * k;
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
                        _phase = Phase.Inactive;
                    }
                    break;
                }
            }
        }

        public void ForceAbort()
        {
            try { Game.TimeScale = 1.0f; } catch { }
            Cleanup();
            _phase = Phase.Inactive;
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
        // tight, falls back to a small offset above the drone.
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
