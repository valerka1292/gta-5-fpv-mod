using System;
using GTA;
using GTA.Math;

namespace FpvDroneMod
{
    // Spec section 9. Single forward raycast per frame.
    internal static class Collision
    {
        public enum Outcome
        {
            Clear,
            ImpactImminent,    // t_to_impact < 0.35s realtime → trigger slow-mo
            ImpactNow          // t_to_impact < dt*1.5 → explode this frame
        }

        public static (Outcome outcome, Vector3 hitPoint, float speed) Step(DroneState s, float dtGame, Ped ignorePed)
        {
            float speed = s.V.Length();
            if (speed < 0.5f)
                return (Outcome.Clear, s.P, speed);

            // ray_length = max(|V| * dt * 1.5, |V| * 0.35)
            float rayLen = Math.Max(speed * dtGame * 1.5f, speed * 0.35f);

            Vector3 end = s.P + s.F * rayLen;

            // Use Everything (511) so vehicles/peds/objects/map/glass all hit.
            // The player ped is excluded — that's the whole point of A7.
            var ray = World.Raycast(s.P, end, IntersectFlags.Everything, ignorePed);
            if (!ray.DidHit)
                return (Outcome.Clear, s.P, speed);

            float hitDist = (ray.HitPosition - s.P).Length();
            float tToImpact = speed > 0.01f ? hitDist / speed : 0.0f;

            if (tToImpact < dtGame * 1.5f)
                return (Outcome.ImpactNow, ray.HitPosition, speed);
            if (tToImpact < Config.TPredict)
                return (Outcome.ImpactImminent, ray.HitPosition, speed);
            return (Outcome.Clear, ray.HitPosition, speed);
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
