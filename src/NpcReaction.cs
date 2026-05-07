using System;
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

            Ped[] nearby = GetNearbyPedsSafe(s.P, PanicRadius, wantCops: false);
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

            Ped[] nearby = GetNearbyPedsSafe(s.P, CopShootRadius, wantCops: true);
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

        private static Ped[] GetNearbyPedsSafe(Vector3 position, float radius, bool wantCops)
        {
            try
            {
                Ped ped = wantCops
                    ? Natives.GetClosestCop(position, radius)
                    : Natives.GetClosestPed(position, radius);

                return ped == null ? NoPeds : new[] { ped };
            }
            catch
            {
                // NPC reactions are non-critical. Avoid SHVDN World.GetNearbyPeds
                // here because some SHVDN/game builds can throw inside
                // NativeMemory.FwScriptGuidPoolTask before managed filtering gets
                // a chance to recover, aborting the whole drone script.
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
