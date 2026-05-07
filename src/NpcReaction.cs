using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;

namespace FpvDroneMod
{
    // Spec: NPC reaction to drone presence.
    //
    // Three behaviours:
    //   A. Nearby pedestrians flee when drone is low and throttle is high.
    //   B. Hovering over a ped for > NpcWarnTime seconds adds a wanted star.
    //   C. Armed cops with line-of-sight to the drone shoot at its position
    //      (visual only — hits are not registered; player ped is invincible).
    internal static class NpcReaction
    {
        // Tuning constants
        private const float PanicAltitude    = 10f;   // м — макс. высота AGL для паники
        private const float PanicRadius      = 15f;   // м — радиус поиска пешеходов
        private const float PanicMinThrottle = 8f;    // м/с — мин. газ для звука пропеллеров
        private const float PanicCooldownSec = 1.2f;  // сек — антиспам вызовов TASK_REACT
        private const float NpcWarnTime      = 4.0f;  // сек — зависание → +1 звезда
        private const float CopShootRadius   = 60f;   // м — радиус поиска копов
        private const float CopShootInterval = 0.12f; // сек — кадение команды стрельбы
        private const int   CopShootDuration = 2500;  // мс — длительность одной команды
        // 0xC6EE5C40 = FIRING_PATTERN_BURST_FIRE (стреляет короткими очередями)
        private const uint  CopFiringPattern  = 0xC6EE5C40;

        private static readonly Model[] NoModelFilter = new Model[0];
        private static readonly Ped[] NoPeds = new Ped[0];

        public static void Update(DroneState s, Ped playerPed, Player player, float dtReal)
        {
            float altAgl = GetAltAgl(s.P);

            UpdatePanicAndWanted(s, playerPed, player, altAgl, dtReal);
            UpdateCopShoot(s, playerPed, altAgl, dtReal);
        }

        // A + B: паника пешеходов и рост розыска
        private static void UpdatePanicAndWanted(
            DroneState s, Ped playerPed, Player player, float altAgl, float dtReal)
        {
            s.PanicCooldown = Math.Max(0f, s.PanicCooldown - dtReal);

            // Паника только при низком полёте и работающих пропеллерах
            bool lowAndLoud = altAgl < PanicAltitude && s.T >= PanicMinThrottle;
            if (!lowAndLoud)
            {
                s.NpcHoverTimer = 0f;
                return;
            }

            Ped[] nearby = GetNearbyPedsSafe(s.P, PanicRadius);
            if (nearby == null || nearby.Length == 0)
            {
                s.NpcHoverTimer = 0f;
                return;
            }

            bool anyClose = false;
            foreach (var p in nearby)
            {
                if (p == null || !p.Exists() || p == playerPed) continue;
                if (Natives.IsPedACop(p)) continue; // копов не пугаем — они стреляют

                anyClose = true;

                // Вызываем TASK_REACT_AND_FLEE_PED с антиспамом
                if (s.PanicCooldown <= 0f)
                {
                    Natives.TaskReactAndFlee(p, playerPed);
                }
            }

            if (s.PanicCooldown <= 0f && anyClose)
                s.PanicCooldown = PanicCooldownSec;

            // Таймер зависания над людьми → розыск
            if (anyClose)
            {
                s.NpcHoverTimer += dtReal;
                if (s.NpcHoverTimer >= NpcWarnTime && Settings.GiveStars)
                {
                    int current = Natives.GetWantedLevel(player);
                    if (current < 2)
                        Natives.SetPlayerWantedLevel(player, current + 1, false);
                    s.NpcHoverTimer = 0f; // сбросить, чтобы не спамить звёздами
                }
            }
            else
            {
                s.NpcHoverTimer = Math.Max(0f, s.NpcHoverTimer - dtReal);
            }
        }

        // C: вооружённые копы стреляют в дрон
        private static void UpdateCopShoot(DroneState s, Ped playerPed, float altAgl, float dtReal)
        {
            s.CopShootTimer = Math.Max(0f, s.CopShootTimer - dtReal);
            if (s.CopShootTimer > 0f) return;

            // Стрельба только если есть розыск
            if (Natives.GetWantedLevel(Game.Player) == 0) return;

            Ped[] nearby = GetNearbyPedsSafe(s.P, CopShootRadius);
            if (nearby == null) return;

            foreach (var p in nearby)
            {
                if (p == null || !p.Exists() || p == playerPed) continue;
                if (!Natives.IsPedACop(p)) continue;

                // Проверяем прямую видимость копа до дрона
                var ray = World.Raycast(p.Position + new Vector3(0, 0, 1f), s.P,
                                        IntersectFlags.Map | IntersectFlags.Objects, playerPed);
                if (ray.DidHit) continue; // геометрия заблокировала — коп не видит дрон

                Natives.TaskShootAtCoord(p, s.P, CopShootDuration, CopFiringPattern);
            }

            s.CopShootTimer = CopShootInterval;
        }

        private static Ped[] GetNearbyPedsSafe(Vector3 position, float radius)
        {
            try
            {
                // Pass an explicit empty model filter instead of using the
                // optional parameter. Some SHVDN builds can pass a null model
                // hash array into NativeMemory.GetGuidsInFwBasePool, which may
                // throw inside the game-thread pool task and abort the script.
                return World.GetNearbyPeds(position, radius, NoModelFilter) ?? NoPeds;
            }
            catch (NullReferenceException)
            {
                return GetNearbyPedsFallback(position, radius);
            }
            catch
            {
                // NPC reactions are non-critical; never let a transient SHVDN
                // pool/enumeration failure terminate the drone script.
                return NoPeds;
            }
        }

        private static Ped[] GetNearbyPedsFallback(Vector3 position, float radius)
        {
            try
            {
                Ped[] all = World.GetAllPeds(NoModelFilter);
                if (all == null || all.Length == 0) return NoPeds;

                float radiusSq = radius * radius;
                var nearby = new List<Ped>();

                foreach (var p in all)
                {
                    try
                    {
                        if (p == null || !p.Exists()) continue;
                        if ((p.Position - position).LengthSquared() > radiusSq) continue;

                        nearby.Add(p);
                    }
                    catch
                    {
                        // Peds can despawn while the pool is being inspected.
                    }
                }

                return nearby.Count == 0 ? NoPeds : nearby.ToArray();
            }
            catch
            {
                return NoPeds;
            }
        }

        private static float GetAltAgl(Vector3 p)
        {
            float groundZ;
            if (!World.GetGroundHeight(p, out groundZ, GetGroundHeightMode.Normal))
                groundZ = p.Z;
            return Math.Max(0f, p.Z - groundZ);
        }
    }
}
