using System;
using GTA;
using GTA.Math;

namespace FpvDroneMod
{
    // Section 9 (rewritten — multi-ray swept-volume contact detection).
    //
    // A single thin raycast through fast objects (cars, ragdoll peds) tunnels
    // through thin geometry — doors, windows, hood corners — and ends up
    // hitting the asphalt under the vehicle. This bug was the root cause of
    // the "exploded under the car instead of into the door" behavior.
    //
    // Fix: instead of one ray, fire a small fan of NumFanRays parallel rays
    // covering the drone's effective hit-volume:
    //
    //   ray 0: along the swept axis (prevP → newP + margin)
    //   ray 1..4: same direction, offset by ±DroneRadius along two axes
    //             perpendicular to the sweep direction
    //
    // The closest hit across all rays is treated as the impact point. Only
    // 5 raycasts per flight tick, all synchronous — same cost as Signal's
    // existing wall-counting pass and orders of magnitude cheaper than going
    // async with ShapeTest.StartTestSweptSphere (which spans frames).
    internal static class Collision
    {
        public enum Outcome
        {
            Clear,
            ImpactNow
        }

        public static (Outcome outcome, Vector3 hitPoint, float speed) Step(
            DroneState s, Vector3 prevP, float dtGame, Ped ignorePed)
        {
            float speed = s.V.Length();

            Vector3 sweep = s.P - prevP;
            float sweepLen = sweep.Length();
            Vector3 dir = sweepLen > 0.001f ? sweep / sweepLen : s.F;

            // Build a stable orthonormal basis perpendicular to the sweep
            // direction. Using world-up as a reference vector except when
            // sweep is nearly vertical, in which case fall back to world-east.
            Vector3 worldUp = new Vector3(0, 0, 1);
            Vector3 axisA = Vector3.Cross(dir, worldUp);
            if (axisA.LengthSquared() < 1e-4f)
                axisA = Vector3.Cross(dir, new Vector3(1, 0, 0));
            axisA = Vector3.Normalize(axisA);
            Vector3 axisB = Vector3.Normalize(Vector3.Cross(dir, axisA));

            float r = Config.DroneRadius;

            // 5-ray fan: centre + 4 perpendicular offsets.
            Vector3[] offsets = new Vector3[]
            {
                Vector3.Zero,
                axisA *  r,
                axisA * -r,
                axisB *  r,
                axisB * -r,
            };

            float bestDist = float.MaxValue;
            Vector3 bestHit = s.P;
            bool didHit = false;

            // Extend each ray's origin one DroneRadius BEHIND prevP along the
            // sweep direction. This handles the audit-#6 edge case: if the
            // drone was already partially clipping into thin geometry
            // (fence, vehicle bodywork) on the previous frame, prevP could
            // be *inside* a mesh — and World.Raycast from inside a closed
            // mesh returns DidHit=false in RAGE. Starting the ray a bit
            // farther back guarantees the origin is outside any
            // sub-DroneRadius-thick obstacle.
            float backExtend = Config.DroneRadius;
            Vector3 originBase = prevP - dir * backExtend;

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 origin = originBase + offsets[i];
                Vector3 endPt  = s.P + offsets[i] + dir * Config.ContactMargin;

                // Everything (511) so vehicles/peds/objects/map/glass all hit.
                var ray = World.Raycast(origin, endPt, IntersectFlags.Everything, ignorePed);
                if (!ray.DidHit) continue;

                // Reject hits that lie behind prevP along the travel
                // direction — those are walls the drone has already passed
                // and would be false positives because we extended origin
                // backward by DroneRadius.
                Vector3 fromPrev = ray.HitPosition - prevP;
                float along = Vector3.Dot(fromPrev, dir);
                float margin = Math.Max(0.05f, Config.DroneRadius * 0.5f);
                if (along < -margin) continue;

                float d = (ray.HitPosition - origin).Length();
                if (d < bestDist)
                {
                    bestDist = d;
                    bestHit  = ray.HitPosition;
                    didHit   = true;
                }
            }

            if (!didHit)
            {
                // Fallback proximity probes for near-overlap cases where sweep
                // ray misses thin geometry at a shallow angle.
                Vector3[] probeOffsets = new Vector3[]
                {
                    dir * r,
                    -dir * r,
                    axisA * r,
                    -axisA * r,
                    axisB * r,
                    -axisB * r,
                };

                for (int i = 0; i < probeOffsets.Length; i++)
                {
                    var probe = World.Raycast(s.P, s.P + probeOffsets[i],
                                              IntersectFlags.Everything, ignorePed);
                    if (!probe.DidHit) continue;

                    Vector3 dp = probe.HitPosition - prevP;
                    if (Vector3.Dot(dp, dir) < -0.05f) continue;

                    float d = (probe.HitPosition - s.P).Length();
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestHit = probe.HitPosition;
                        didHit = true;
                    }
                }
            }

            if (!didHit)
                return (Outcome.Clear, s.P, speed);

            return (Outcome.ImpactNow, bestHit, speed);
        }

        // Spec 9.2 — explosion at hit point + Battlefield-style impulse on
        // nearby vehicles, both scaled by impact speed. The chosen explosion
        // *preset* is preserved (whatever the menu selected); we only multiply
        // its built-in damage by a speed-derived factor.
        //
        // approachDir is the unit vector along which the drone hit the
        // surface (prevP → hit). For the primary target (vehicle very close
        // to hit_point) we blend approachDir into the impulse direction so
        // a head-on ram actually shoves the truck *along the kamikaze's
        // velocity vector* instead of just radially outward.
        public static void Detonate(Vector3 hitPoint, Vector3 approachDir, float speed)
        {
            // Speed → scale: 1.0 at v_min, DamageScaleMax at TMax.
            float vNorm = (speed - Config.DamageScaleMinSpeed)
                          / Math.Max(0.01f, Config.TMax - Config.DamageScaleMinSpeed);
            if (vNorm < 0f) vNorm = 0f;
            if (vNorm > 1f) vNorm = 1f;
            float damageScale = 1.0f + (Config.DamageScaleMax - 1.0f) * vNorm;

            Natives.AddExplosionUnowned(
                hitPoint,
                explosionType: Settings.CurrentExplosionId,
                damageScale: damageScale,
                audible: true,
                invisible: false,
                cameraShake: 1.0f);

            // Apply a directional impulse to every vehicle within
            // VehicleImpulseRadius of the hit. Force magnitude scales with
            // impact speed and falls off linearly with distance from hit.
            // Direction = world-down inversion: push along the sweep
            // direction (we approximate by hit_point − vehicle_centre, which
            // points outward like a real explosion shockwave).
            try
            {
                Vehicle[] nearby = World.GetNearbyVehicles(hitPoint, Config.VehicleImpulseRadius);
                if (nearby != null)
                {
                    foreach (var v in nearby)
                    {
                        if (v == null || !v.Exists()) continue;

                        Vector3 from = hitPoint;
                        Vector3 to   = v.Position;
                        Vector3 push = to - from;
                        float dist = push.Length();
                        if (dist < 0.01f) continue;

                        float falloff = 1.0f - (dist / Config.VehicleImpulseRadius);
                        if (falloff < 0f) continue;

                        Vector3 radialDir = push / dist;

                        // Blend approachDir (the drone's velocity direction
                        // at impact) with the radial outward direction. The
                        // closer the vehicle is to the hit_point — i.e. the
                        // closer it is to *being the target we rammed* — the
                        // more we weight approachDir. Vehicles further out
                        // get a near-pure radial shockwave.
                        float approachWeight = falloff; // 1.0 at hit, 0 at radius
                        Vector3 blendedDir = radialDir * (1.0f - approachWeight)
                                           + approachDir * approachWeight;
                        if (blendedDir.LengthSquared() > 0.000001f)
                            blendedDir = Vector3.Normalize(blendedDir);

                        // +Z component so vehicles get lifted, not just shoved
                        // along the ground (matches RAGE's own explosion feel).
                        blendedDir = blendedDir + new Vector3(0, 0, 0.35f);
                        if (blendedDir.Z < 0f)
                            blendedDir = new Vector3(blendedDir.X, blendedDir.Y, 0f);
                        if (blendedDir.LengthSquared() > 0.000001f)
                            blendedDir = Vector3.Normalize(blendedDir);

                        Vector3 force = blendedDir
                                        * Config.VehicleImpulsePerSpeed
                                        * speed
                                        * falloff;

                        // Apply force at the actual hit point (in world
                        // space, then converted to entity-local via
                        // GET_OFFSET_FROM_ENTITY_GIVEN_WORLD_COORDS) so the
                        // engine generates real torque — a side hit makes
                        // the truck flip on its long axis, a top hit slams
                        // the roof down, etc. Without this, force at
                        // entity.Position is pure translation and trucks
                        // just glide instead of rolling.
                        Vector3 localOffset = Natives.GetOffsetFromEntityGivenWorldCoords(v, hitPoint);
                        Natives.ApplyForceToEntity(v, force, localOffset, forceType: 1);
                    }
                }
            }
            catch
            {
                // Impulse is a polish layer; never let an exception here
                // poison the cinematic phase.
            }
        }
    }
}
