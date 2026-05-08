using System;
using GTA;
using GTA.Math;

namespace FpvDroneMod
{
    // Section 5 — Autopilot (loitering-munition logic).
    //
    // TRACKING mode  (ПКМ):
    //   Drone holds a fixed 3-D offset behind and above the target:
    //     desiredPos = target.P + rearDir * TrackRearOffset + (0,0,TrackAltOffset)
    //   rearDir is opposite to the target's horizontal velocity, or opposite to
    //   drone→target when the target is near-stationary.  Speed is proportional
    //   to distance (ramps up far away, bleeds off inside the hover bubble).
    //   A 5-ray obstacle fan is cast each frame and blends an avoidance
    //   component into the desired movement direction so the drone steers around
    //   trees, poles, buildings, etc. that are not the locked target.
    //
    // ATTACKING mode (ЛКМ while tracking):
    //   Throttle ramps via Config.ThrottleRate exactly like holding T — smooth,
    //   not a snap. Aim uses lead-prediction with a softer initial slew that
    //   stiffens once the drone is already pointed near the target.
    //
    // LoS dropout: if the target stays behind solid geometry for >LosDropTime s
    // the lock is released (same as before, but now also ignores Objects layer
    // so a parked car between drone and target doesn't false-drop the lock
    // on a distant vehicle).
    internal static class Autopilot
    {
        // ── Tracking ──────────────────────────────────────────────────────
        private const float TrackAltOffset   = 30f;   // m above target
        private const float TrackRearOffset  = 22f;   // m behind target; look-down angle is atan2(30,22)=53.7deg
        private const float TrackHoverRadius = 4f;    // м — зона зависания
        private const float TrackSpeedFrac   = 0.72f; // доля TMax при слежении
        private const float TrackAimSpeed    = 2.2f;  // плавность поворота

        // ── Attacking ─────────────────────────────────────────────────────
        // Начальная мягкая слежка; как только нос смотрит в ±20° от цели —
        // стягиваемся жёстче.
        private const float AttackAimSpeedSoft = 3.5f;
        private const float AttackAimSpeedHard = 7.0f;
        private const float AttackAimBlendDeg  = 20f;  // порог переключения
        private const float AttackMaxTti        = 5.0f; // c — ограничение упреждения
        private const float AttackBrakeDist     = 20f;  // м — начало торможения
        private const float AttackMinSpeedFrac  = 0.40f; // не ниже 40% в атаке

        // ── LoS ───────────────────────────────────────────────────────────
        private const float LosDropTime = 2.0f;

        // ── Obstacle avoidance ────────────────────────────────────────────
        // 5 лучей: вперёд, влево, вправо, вверх, чуть вниз-вперёд.
        // Дальность луча вперёд масштабируется со скоростью дрона, чтобы
        // медленный вираж не генерировал ложных уклонений.
        private const float ObstFwdBase   = 16f;   // м вперёд (базовая)
        private const float ObstFwdExtra  = 0.60f; // +0.6 м на каждый м/с
        private const float ObstSideLen   = 10f;   // м по бокам
        private const float ObstUpLen     =  7f;   // м вверх
        private const float ObstStrength  = 3.2f;  // сила уклонения
        // Минимальная доля зоны до препятствия, ниже которой начинается уклонение
        // (т.е. мы уже не реагируем на объекты дальше 100% длины луча — там urgency=0).
        private const float ObstDeadzone  = 0.15f; // пропускаем urgency < 0.15

        public static void Update(DroneState s, Ped playerPed, float dtReal)
        {
            if (s.AutopilotMode == AutoPilotState.Off) return;

            if (s.LockedTarget == null || !s.LockedTarget.Exists() || s.LockedTarget.IsDead)
            {
                ReleaseTarget(s);
                return;
            }

            Vector3 tgtPos = s.LockedTarget.Position;
            if (!IsFiniteVec(tgtPos))
            {
                ReleaseTarget(s);
                return;
            }

            if (HasLineOfSight(s, s.LockedTarget)) s.TargetLostTimer = 0f;
            else                                   s.TargetLostTimer += dtReal;

            if (s.TargetLostTimer > LosDropTime)
            {
                ReleaseTarget(s);
                return;
            }

            switch (s.AutopilotMode)
            {
                case AutoPilotState.Tracking:
                    UpdateTracking(s, tgtPos, playerPed, dtReal);
                    break;
                case AutoPilotState.Attacking:
                    UpdateAttacking(s, tgtPos, dtReal);
                    break;
            }
        }

        // ── Tracking ──────────────────────────────────────────────────────
        private static void UpdateTracking(
            DroneState s, Vector3 tgtPos, Ped playerPed, float dtReal)
        {
            // Вектор "позади цели": противоположен горизонтальной скорости цели,
            // или противоположен drone→target, если цель почти стоит.
            Vector3 velXY = new Vector3(
                s.LockedTarget.Velocity.X,
                s.LockedTarget.Velocity.Y, 0f);

            Vector3 rawRearDir;
            if (velXY.LengthSquared() > 1.5f)
            {
                rawRearDir = -Vector3.Normalize(velXY);
            }
            else
            {
                Vector3 droneForwardXY = new Vector3(s.F.X, s.F.Y, 0f);
                rawRearDir = droneForwardXY.LengthSquared() > 0.001f
                    ? -Vector3.Normalize(droneForwardXY)
                    : new Vector3(0f, -1f, 0f);
            }
            if (!IsFiniteVec(rawRearDir) || rawRearDir.LengthSquared() < 0.001f)
                rawRearDir = new Vector3(0f, -1f, 0f);

            if (!IsFiniteVec(s.SmoothedRearDir) || s.SmoothedRearDir.LengthSquared() < 0.001f)
                s.SmoothedRearDir = rawRearDir;

            float tgtSpeed = s.LockedTarget.Velocity.Length();
            if (!IsFinite(tgtSpeed)) tgtSpeed = 0f;
            float alpha = Physics.Clamp01(0.04f + tgtSpeed * 0.002f) * (1f + dtReal * 2f);
            Vector3 blendedRear = s.SmoothedRearDir + (rawRearDir - s.SmoothedRearDir) * alpha;
            if (IsFiniteVec(blendedRear) && blendedRear.LengthSquared() > 0.001f)
                s.SmoothedRearDir = Vector3.Normalize(blendedRear);

            // Целевая позиция зависания: сзади и выше цели.
            Vector3 desiredPos = tgtPos
                + new Vector3(0f, 0f, TrackAltOffset)
                + s.SmoothedRearDir * TrackRearOffset;

            Vector3 toDesired = desiredPos - s.P;
            float   dist      = toDesired.Length();

            // Базовое направление движения — к желаемой точке зависания.
            Vector3 moveDir = dist > 0.25f
                ? Vector3.Normalize(toDesired)
                : s.F; // уже на месте — держим текущий heading

            // Уклонение от препятствий (деревья, стены, столбы).
            // В режиме атаки не вызываем — дрон должен идти прямо.
            moveDir = AvoidObstacles(s, moveDir, playerPed);
            s.AutopilotFlightDir = moveDir;

            // Пропорциональная скорость: максимум далеко, тормозим у зависания.
            float cruiseSpeed = Settings.CurrentProfile.TMax * TrackSpeedFrac;
            float speed = Math.Min(cruiseSpeed, dist * 1.8f);
            if (dist < TrackHoverRadius)
                speed *= dist / TrackHoverRadius;

            s.T = Physics.Clamp(speed, 0f, Settings.CurrentProfile.TMax);

            Vector3 toTarget = tgtPos - s.P;
            Vector3 aimDir = toTarget.LengthSquared() > 0.001f
                ? Vector3.Normalize(toTarget)
                : moveDir;
            AimAt(s, aimDir, dtReal, TrackAimSpeed);
        }

        // ── Attacking ──────────────────────────────────────────────────────
        private static void UpdateAttacking(
            DroneState s, Vector3 tgtPos, float dtReal)
        {
            s.AutopilotFlightDir = null;

            // Упреждение: вычислить точку встречи.
            Vector3 toTgt = tgtPos - s.P;
            float   dist  = toTgt.Length();
            if (dist <= 0.001f) return;

            Vector3 dirToTgt = toTgt / dist;
            Vector3 relVel = IsFiniteVec(s.V) ? s.V - s.LockedTarget.Velocity : -s.LockedTarget.Velocity;
            float closing = Vector3.Dot(relVel, dirToTgt);

            Vector3 aimDir;
            if (closing <= 0.5f)
            {
                // Если не сближаемся, целимся в текущую позицию —
                // длинное упреждение только уводит дрон мимо цели.
                aimDir = dirToTgt;
            }
            else
            {
                float tti = Math.Min(dist / closing, AttackMaxTti);
                Vector3 predictedPos = tgtPos + s.LockedTarget.Velocity * tti;
                Vector3 aimVec = predictedPos - s.P;
                aimDir = aimVec.LengthSquared() > 0.001f ? Vector3.Normalize(aimVec) : dirToTgt;
            }

            // Мягкий старт: когда нос ещё далеко от цели — сначала разворачиваемся
            // плавно, потом жёстче когда уже почти наведено.
            float dotForward = Vector3.Dot(s.F, aimDir);
            float blendCos = (float)Math.Cos(AttackAimBlendDeg * MathF.Deg2Rad);
            float t = Physics.Clamp01((dotForward - blendCos) / (1f - blendCos));
            float aimSpeed = AttackAimSpeedSoft + (AttackAimSpeedHard - AttackAimSpeedSoft) * t;

            AimAt(s, aimDir, dtReal, aimSpeed);

            float speedFraction = Physics.Clamp01((dist - 5f) / AttackBrakeDist);
            float desiredSpeed = Math.Max(
                Settings.CurrentProfile.TMax * AttackMinSpeedFrac,
                Settings.CurrentProfile.TMax * speedFraction);
            if (s.T > desiredSpeed)
                Physics.AdjustThrottle(s, -Config.ThrottleRate * dtReal);
            else
                Physics.AdjustThrottle(s, Config.ThrottleRate * dtReal);
        }

        // ── Obstacle avoidance ─────────────────────────────────────────────
        // Возвращает moveDir с подмешанным вектором уклонения от геометрии.
        private static Vector3 AvoidObstacles(
            DroneState s, Vector3 moveDir, Ped ignorePed)
        {
            if (!IsFiniteVec(moveDir) || moveDir.LengthSquared() < 1e-6f)
                return s.F;
            moveDir = Vector3.Normalize(moveDir);
            // Ортонормальный базис относительно вектора движения.
            Vector3 worldUp = new Vector3(0f, 0f, 1f);
            Vector3 right   = Vector3.Cross(moveDir, worldUp);
            if (right.LengthSquared() < 1e-4f)
                right = Vector3.Cross(moveDir, new Vector3(1f, 0f, 0f));
            right = Vector3.Normalize(right);

            float speed     = s.V.Length();
            if (!IsFinite(speed)) speed = 0f;
            float fwdLen    = ObstFwdBase + speed * ObstFwdExtra;
            float diagonalLen = fwdLen * 0.65f;

            // 5 лучей: (направление, длина, вес уклонения)
            Vector3[] probeDir = {
                moveDir,
                -right,
                right,
                worldUp,
                Vector3.Normalize(moveDir - worldUp * 0.35f)
            };
            float[] probeLen = { fwdLen, ObstSideLen, ObstSideLen, ObstUpLen, diagonalLen };
            float[] probeW   = { 1.4f,   0.8f,        0.8f,        0.55f,     0.6f };

            int i = s.SensorNextZone;
            if (i < 0 || i >= probeDir.Length) i = 0;
            s.SensorNextZone = (i + 1) % probeDir.Length;
            s.SensorClearances[i] = probeLen[i];
            s.SensorPushDirs[i] = -probeDir[i];

            var ray = World.Raycast(
                s.P,
                s.P + probeDir[i] * probeLen[i],
                IntersectFlags.Map | IntersectFlags.Objects,
                ignorePed);

            if (ray.DidHit && (s.LockedTarget == null || ray.HitEntity != s.LockedTarget))
            {
                float hitDist = (ray.HitPosition - s.P).Length();
                s.SensorClearances[i] = IsFinite(hitDist) ? hitDist : probeLen[i];

                Vector3 normal = ray.SurfaceNormal;
                s.SensorPushDirs[i] = IsFiniteVec(normal) && normal.LengthSquared() > 0.5f
                    ? Vector3.Normalize(normal)
                    : -probeDir[i];
            }

            if (i == probeDir.Length - 1) s.SensorReady = true;
            if (!s.SensorReady) return moveDir;

            Vector3 avoid = Vector3.Zero;

            for (int j = 0; j < probeDir.Length; j++)
            {
                float clearance = s.SensorClearances[j] > 0f ? s.SensorClearances[j] : probeLen[j];
                float urgency = 1f - Physics.Clamp01(clearance / probeLen[j]); // 0..1, stronger nearby
                if (urgency < ObstDeadzone) continue;

                Vector3 pushDir = s.SensorPushDirs[j];
                if (!IsFiniteVec(pushDir) || pushDir.LengthSquared() < 0.001f)
                    pushDir = -probeDir[j];
                else
                    pushDir = Vector3.Normalize(pushDir);

                avoid += pushDir * (urgency * probeW[j] * ObstStrength);
            }

            if (avoid.LengthSquared() < 0.001f)
                return moveDir;

            Vector3 blended = moveDir + avoid;
            return blended.LengthSquared() > 0.001f
                ? Vector3.Normalize(blended)
                : Vector3.Normalize(avoid); // полностью заблокировано — разворот
        }

        // ── AimAt ──────────────────────────────────────────────────────────
        private static void AimAt(DroneState s, Vector3 dir, float dt, float speed)
        {
            if (!IsFiniteVec(dir) || dir.LengthSquared() < 1e-6f) return;
            dir = Vector3.Normalize(dir);

            float desiredPsi   = (float)Math.Atan2(-dir.X, dir.Y);
            float safeZ        = Math.Max(-0.999f, Math.Min(0.999f, dir.Z));
            float desiredTheta = (float)Math.Asin(safeZ);

            float dPsi = NormalizeAngleSigned(desiredPsi - s.Psi);

            float k = Math.Min(1f, dt * speed);
            s.Psi   += dPsi * k;
            s.Theta  = Physics.Clamp(
                s.Theta + (desiredTheta - s.Theta) * k,
                -Settings.CurrentProfile.ThetaMax,
                Settings.CurrentProfile.ThetaMax);
        }

        public static void ReleaseTarget(DroneState s)
        {
            s.AutopilotMode = AutoPilotState.Off;
            s.LockedTarget = null;
            s.TargetLostTimer = 0f;
            s.AutopilotFlightDir = null;
            s.SensorNextZone = 0;
            s.SensorReady = false;
        }

        private static bool HasLineOfSight(DroneState s, Entity target)
        {
            Vector3 center = target.Position;
            Vector3 top = center + new Vector3(0f, 0f, 1.5f);
            Vector3 forward = target.ForwardVector;
            Vector3 front = center + (IsFiniteVec(forward) ? forward * 2f : Vector3.Zero);

            Vector3[] points = { center, top, front };
            for (int i = 0; i < points.Length; i++)
            {
                var ray = World.Raycast(
                    s.P,
                    points[i],
                    IntersectFlags.Map,
                    target);
                if (!ray.DidHit) return true;
            }
            return false;
        }

        private static float NormalizeAngleSigned(float angle)
        {
            float twoPi = 2f * MathF.Pi;
            float wrapped = angle + MathF.Pi;
            wrapped = wrapped - twoPi * (float)Math.Floor(wrapped / twoPi);
            return wrapped - MathF.Pi;
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool IsFiniteVec(Vector3 v) => IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
    }
}
