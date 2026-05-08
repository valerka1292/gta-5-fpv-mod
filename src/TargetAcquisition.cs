using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace FpvDroneMod
{
    internal static class TargetAcquisition
    {
        private const float LockConeHalfDeg = 14f;
        private const float MaxRange        = 1200f; // Увеличена дальность захвата
        private const float ConfirmTime     = 0.12f;

        private static Entity _candidate;
        private static float  _candidateTimer;

        private static int    _shapeTestHandle = 0;
        private static Entity _lastFoundEntity = null;

        public static void Update(DroneState s, Ped playerPed, float dtReal)
        {
            if (s.AutopilotMode != AutoPilotState.Off)
            {
                Reset();
                s.PotentialTarget = null;
                return;
            }

            Vector3 origin  = s.P;
            Vector3 forward = s.F;

            // 1. Асинхронный поиск цели капсулой
            if (_shapeTestHandle == 0)
            {
                Vector3 startPos = origin + forward * 1.0f;
                Vector3 endPos   = origin + forward * MaxRange;
                float capsuleRadius = 4.0f;

                // Флаг 10 = Vehicles (2) + Peds (8)
                _shapeTestHandle = Function.Call<int>(Hash.START_SHAPE_TEST_CAPSULE,
                    startPos.X, startPos.Y, startPos.Z,
                    endPos.X, endPos.Y, endPos.Z,
                    capsuleRadius,
                    10,
                    playerPed.Handle,
                    7);
            }
            else
            {
                var hitArg       = new OutputArgument();
                var endCoordsArg = new OutputArgument();
                var normalArg    = new OutputArgument();
                var entityHitArg = new OutputArgument();

                int status = Function.Call<int>(Hash.GET_SHAPE_TEST_RESULT,
                    _shapeTestHandle, hitArg, endCoordsArg, normalArg, entityHitArg);

                if (status == 2) // Тест завершен
                {
                    _shapeTestHandle = 0;
                    _lastFoundEntity = null;

                    bool hit = hitArg.GetResult<bool>();
                    if (hit)
                    {
                        int handle = entityHitArg.GetResult<int>();
                        if (handle != 0)
                        {
                            Entity e = Entity.FromHandle(handle);
                            if (e != null && e.Exists() && !e.IsDead && e.Handle != playerPed.Handle)
                            {
                                // Отсекаем цели, которые оказались слишком сбоку от центра
                                Vector3 dirToTarget = Vector3.Normalize(e.Position - origin);
                                if (Vector3.Dot(forward, dirToTarget) > 0.4f)
                                {
                                    // Рейкаст убран: сразу подтверждаем находку
                                    _lastFoundEntity = e;
                                }
                            }
                        }
                    }
                }
                else if (status == 0) // Тест не удался
                {
                    _shapeTestHandle = 0;
                }
            }

            // 2. Таймер удержания (гистерезис)
            if (_lastFoundEntity != null && _lastFoundEntity == _candidate)
            {
                _candidateTimer += dtReal;
            }
            else
            {
                _candidate      = _lastFoundEntity;
                _candidateTimer = _lastFoundEntity != null ? dtReal : 0f;
            }

            s.PotentialTarget = (_candidateTimer >= ConfirmTime) ? _candidate : null;
        }

        public static bool IsLockedInRetentionCone(DroneState s)
        {
            if (s.LockedTarget == null || !s.LockedTarget.Exists()) return false;
            float angle = AngleBetween(s.P, s.F, s.LockedTarget.Position);
            return angle <= LockConeHalfDeg * MathF.Deg2Rad;
        }

        private static float AngleBetween(Vector3 origin, Vector3 forward, Vector3 target)
        {
            Vector3 dir = target - origin;
            if (dir.LengthSquared() < 0.001f) return 0f;
            dir = Vector3.Normalize(dir);
            float dot = Vector3.Dot(forward, dir);
            dot = Math.Max(-1f, Math.Min(1f, dot));
            return (float)Math.Acos(dot);
        }

        public static void Reset()
        {
            _candidate       = null;
            _candidateTimer  = 0f;
            _lastFoundEntity = null;
            _shapeTestHandle = 0;
        }
    }
}