using System;
using GTA;
using GTA.Math;

namespace FpvDroneMod
{
    // Section 9 (rewritten). Pure contact detection: every frame we raycast
    // the *exact* travel of the drone for the upcoming step plus a tiny
    // safety margin. No predictive look-ahead, no early slow-mo trigger —
    // the player flies at full speed until the camera literally touches a
    // surface, and only then does the cinematic kick in.
    internal static class Collision
    {
        public enum Outcome
        {
            Clear,
            ImpactNow   // ray hit within next-frame travel distance + margin
        }

        // Swept-volume contact ray. Cast from the position the drone occupied
        // BEFORE this frame's IntegrateMotion to its current (post-integration)
        // position, plus a short ContactMargin "feeler" past the new position.
        // This is what kills the tunneling-through-cars bug at high speeds:
        // even if the drone covers several metres in one frame, the ray
        // covers the entire swept path and the first surface it touches is
        // the one that detonates — exactly where the camera entered the
        // texture under the angle of approach.
        public static (Outcome outcome, Vector3 hitPoint, float speed) Step(
            DroneState s, Vector3 prevP, float dtGame, Ped ignorePed)
        {
            float speed = s.V.Length();

            Vector3 sweep = s.P - prevP;
            float sweepLen = sweep.Length();
            Vector3 dir = sweepLen > 0.001f ? sweep / sweepLen : s.F;

            // End the ray a short ContactMargin past the new position so that
            // (a) we still detect surfaces we're about to clip into when
            // hovering / very slow, and (b) we never miss a wall the drone
            // would penetrate within one more sub-pixel of motion.
            Vector3 end = s.P + dir * Config.ContactMargin;

            // Everything (511) so vehicles/peds/objects/map/glass all hit.
            // The player ped is excluded — see PlayerGuard A7.
            var ray = World.Raycast(prevP, end, IntersectFlags.Everything, ignorePed);
            if (!ray.DidHit)
                return (Outcome.Clear, s.P, speed);

            return (Outcome.ImpactNow, ray.HitPosition, speed);
        }

        // Spec 9.2 — explosion at hit point.
        public static void Detonate(Vector3 hitPoint, float speed)
        {
            float damageScale = speed / Config.TMax;
            if (damageScale < 0.4f) damageScale = 0.4f;
            if (damageScale > 1.0f) damageScale = 1.0f;

            Natives.AddExplosionUnowned(
                hitPoint,
                explosionType: Settings.CurrentExplosionId,
                damageScale: damageScale,
                audible: true,
                invisible: false,
                cameraShake: 1.0f);
        }
    }
}
