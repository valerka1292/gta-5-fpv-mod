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

        public static (Outcome outcome, Vector3 hitPoint, Vector3 hitNormal, Entity hitEntity, float speed) Step(
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
            Vector3 bestNormal = -dir;
            Entity bestEntity = null;
            bool didHit = false;

            // Start from the real swept volume front plane (prevP + offsets).
            // We intentionally do NOT extend origin backwards, because that
            // causes false-positive hits on surfaces the drone is already
            // moving away from (e.g. wall behind a reversing drift).
            Vector3 originBase = prevP;

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
                    bestNormal = ray.SurfaceNormal.LengthSquared() > 0.0001f
                        ? Vector3.Normalize(ray.SurfaceNormal)
                        : -dir;
                    bestEntity = ray.HitEntity;
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
                        bestNormal = probe.SurfaceNormal.LengthSquared() > 0.0001f
                            ? Vector3.Normalize(probe.SurfaceNormal)
                            : -dir;
                        bestEntity = probe.HitEntity;
                        didHit = true;
                    }
                }
            }

            if (!didHit)
                return (Outcome.Clear, s.P, -dir, null, speed);

            return (Outcome.ImpactNow, bestHit, bestNormal, bestEntity, speed);
        }

        // Spec 9.2 — explosion at hit point + preset-driven physical impulse
        // on nearby vehicles. HP damage is calculated independently from the
        // FORCE / IMPULSE preset; the preset controls shock radius, force,
        // camera shake, audibility, and visibility.
        //
        // approachDir is retained for call-site compatibility; the shockwave
        // force is now computed radially as entity.Position - hitPoint.
        public static void Detonate(Vector3 hitPoint, Vector3 approachDir, float speed)
        {
            // HP damage is intentionally independent from the impulse preset.
            int idx = Settings.DamageOverrideIndex;
            if (idx < 0) idx = 0;
            if (idx >= Settings.DamageValues.Length) idx = Settings.DamageValues.Length - 1;
            float hpValue = Settings.DamageValues[idx];

            // Convert the selected HP value into GTA's damageScale
            // (1000 HP ~= 5.0 scale), then add only the existing speed bonus.
            float baseScale = hpValue * 0.005f;
            float vNorm = (speed - Config.DamageScaleMinSpeed)
                          / Math.Max(0.01f, Settings.CurrentProfile.TMax - Config.DamageScaleMinSpeed);
            vNorm = Math.Max(0f, Math.Min(1f, vNorm));
            float damageScale = baseScale * (1.0f + 0.2f * vNorm);

            Settings.ImpulsePreset impulsePreset = Settings.CurrentImpulsePreset;

            Ped ownerPed = null;
            if (Settings.GiveStars)
            {
                try { ownerPed = Game.Player?.Character; } catch { ownerPed = null; }
            }

            Natives.AddOwnedExplosion(
                ownerPed,
                hitPoint,
                explosionType: Settings.CurrentExplosionId,
                damageScale: damageScale,
                audible: impulsePreset.Audible,
                invisible: impulsePreset.Invisible,
                cameraShake: impulsePreset.CameraShake);

            // Apply the impulse preset as a separate physical shockwave layer.
            // Direction is purely radial from the actual hit point to each
            // entity. If the hit point is above an entity and the raw radial
            // vector points down into the ground, flip Z so vehicles are lifted
            // instead of being pinned downward.
            if (impulsePreset.ForceScale <= 0f || impulsePreset.Radius <= 0f)
                return;

            try
            {
                Vehicle[] nearby = World.GetNearbyVehicles(hitPoint, impulsePreset.Radius);
                if (nearby != null)
                {
                    foreach (var v in nearby)
                    {
                        if (v == null || !v.Exists()) continue;

                        Vector3 radial = v.Position - hitPoint;
                        float dist = radial.Length();
                        if (dist < 0.01f || dist > impulsePreset.Radius) continue;

                        if (radial.Z < 0f)
                            radial.Z = -radial.Z;

                        if (radial.LengthSquared() <= 0.000001f) continue;

                        Vector3 radialDir = Vector3.Normalize(radial);
                        Vector3 force = radialDir
                                        * Config.VehicleImpulsePerSpeed
                                        * impulsePreset.ForceScale
                                        * speed;

                        // Apply force at the actual hit point converted to the
                        // entity's local coordinates. This off-centre impulse
                        // creates torque, causing flips and rolls instead of
                        // simple linear sliding.
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

        // Physical contact impulse from drone mass and impact geometry.
        // We compute the closing speed along surface normal and apply an
        // impulse at hit point before the explosion sequence.
        public static void ApplyKineticImpactImpulse(
            Entity hitEntity,
            Vector3 hitPoint,
            Vector3 hitNormal,
            Vector3 droneVelocity)
        {
            try
            {
                if (hitEntity == null || !hitEntity.Exists()) return;

                Vector3 relVel = droneVelocity - hitEntity.Velocity;
                float closingSpeed = -Vector3.Dot(relVel, hitNormal);
                if (closingSpeed <= 0.05f) return;

                Settings.ImpulsePreset impulsePreset = Settings.CurrentImpulsePreset;

                float j = Config.DroneMassKg
                          * closingSpeed
                          * Config.ImpactImpulseScale
                          * impulsePreset.ForceScale;
                if (j <= 0.01f) return;

                Vector3 impulse = relVel.LengthSquared() > 0.001f
                    ? Vector3.Normalize(relVel) * j
                    : -hitNormal * j;

                Vector3 localOffset = Natives.GetOffsetFromEntityGivenWorldCoords(hitEntity, hitPoint);
                Natives.ApplyForceToEntity(hitEntity, impulse, localOffset, forceType: 1);
            }
            catch
            {
                // Impact impulse should never crash flight loop.
            }
        }
    }
}
