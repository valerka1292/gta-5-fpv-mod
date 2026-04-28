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

        public static (Outcome outcome, Vector3 hitPoint, float speed) Step(DroneState s, float dtGame, Ped ignorePed)
        {
            float speed = s.V.Length();

            // Even with very low velocity we still want a thin "feeler" ray so
            // that ramming into something at near-stationary speeds still
            // detonates instead of penetrating.
            float travel = speed * dtGame * 1.5f;
            float rayLen = Math.Max(travel + Config.ContactMargin, Config.ContactMargin);

            Vector3 end = s.P + s.F * rayLen;

            // Everything (511) so vehicles/peds/objects/map/glass all hit.
            // The player ped is excluded — see PlayerGuard A7.
            var ray = World.Raycast(s.P, end, IntersectFlags.Everything, ignorePed);
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

            // Type 2 = Grenade
            Natives.AddExplosionUnowned(
                hitPoint,
                explosionType: 2,
                damageScale: damageScale,
                audible: true,
                invisible: false,
                cameraShake: 1.0f);
        }
    }
}
