using System;
using GTA;
using GTA.Math;

namespace FpvDroneMod
{
    internal static class Autopilot
    {
        public static void Update(DroneState s, float dtReal)
        {
            if (s.AutopilotMode == AutoPilotState.Off) return;

            // If the target is destroyed or disappears, drop the lock.
            if (s.LockedTarget == null || !s.LockedTarget.Exists() || s.LockedTarget.IsDead)
            {
                s.AutopilotMode = AutoPilotState.Off;
                s.LockedTarget = null;
                return;
            }

            // Check line of sight. If the target stays hidden behind map geometry for >2s, drop the lock.
            var ray = World.Raycast(s.P, s.LockedTarget.Position, IntersectFlags.Map, s.LockedTarget);
            if (ray.DidHit) s.TargetLostTimer += dtReal;
            else s.TargetLostTimer = 0f;

            if (s.TargetLostTimer > 2.0f)
            {
                s.AutopilotMode = AutoPilotState.Off;
                s.LockedTarget = null;
                return;
            }

            Vector3 targetPos = s.LockedTarget.Position;
            Vector3 dronePos = s.P;

            if (s.AutopilotMode == AutoPilotState.Tracking)
            {
                // Loiter above the target at roughly +30m AGL relative to the target.
                float targetZ = targetPos.Z + 30f;
                float altDiff = targetZ - dronePos.Z;
                float desiredThrottle = 15f + altDiff * 0.8f;
                s.T = Physics.Clamp(desiredThrottle, Config.TMin, Settings.CurrentProfile.TMax);

                Vector3 dir = targetPos - dronePos;
                if (dir.LengthSquared() > 0.001f)
                    AimAt(s, Vector3.Normalize(dir), dtReal, 3.0f);
            }
            else if (s.AutopilotMode == AutoPilotState.Attacking)
            {
                // Terminal dive: full throttle.
                s.T = Settings.CurrentProfile.TMax;

                // Kinetic lead: aim at the predicted intercept point using current closing time.
                float dist = (targetPos - dronePos).Length();
                float closingSpeed = Math.Max(20f, s.V.Length());
                float timeToImpact = dist / closingSpeed;
                Vector3 predictedPos = targetPos + (s.LockedTarget.Velocity * timeToImpact);

                Vector3 dir = predictedPos - dronePos;
                if (dir.LengthSquared() > 0.001f)
                    AimAt(s, Vector3.Normalize(dir), dtReal, 8.0f);
            }
        }

        private static void AimAt(DroneState s, Vector3 dir, float dt, float speed)
        {
            float desiredPsi = (float)Math.Atan2(-dir.X, dir.Y);
            float desiredTheta = (float)Math.Asin(dir.Z);

            // Avoid spin when crossing ±PI.
            float dPsi = desiredPsi - s.Psi;
            while (dPsi > MathF.Pi) dPsi -= 2 * MathF.Pi;
            while (dPsi < -MathF.Pi) dPsi += 2 * MathF.Pi;

            float k = Math.Min(1f, dt * speed);
            s.Psi += dPsi * k;
            s.Theta += (desiredTheta - s.Theta) * k;
        }
    }
}
