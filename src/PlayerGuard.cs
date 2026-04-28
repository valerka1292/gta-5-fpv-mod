using GTA;
using GTA.Math;
using GTA.Native;

namespace FpvDroneMod
{
    // Saves player state on flight start and restores it on flight end.
    // Implements spec sections 2.1 (preflight), 2.2 (init) and 2.4 (cleanup).
    internal static class PlayerGuard
    {
        // Returns null on success, or a human-readable rejection reason.
        public static string PreflightCheck(Player player, Ped ped)
        {
            if (Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS))
                return "Not available in multiplayer";
            if (Function.Call<bool>(Hash.IS_PED_IN_ANY_VEHICLE, ped.Handle, false))
                return "Cannot launch from a vehicle";
            if (Natives.IsCutscenePlaying() || Game.IsCutsceneActive)
                return "Cannot launch during a cutscene";
            if (ped.IsDead || Function.Call<bool>(Hash.IS_PED_DEAD_OR_DYING, ped.Handle, true))
                return "Cannot launch while dead";
            if (Natives.IsPedRagdoll(ped))
                return "Cannot launch while in ragdoll";
            if (Natives.IsPedSwimming(ped))
                return "Cannot launch while swimming";

            // Roof check: ray straight up.
            var p = ped.Position;
            var ray = World.Raycast(p, p + new Vector3(0, 0, Config.SpawnHeight),
                                    IntersectFlags.Map, ped);
            if (ray.DidHit) return "Cannot launch — roof / overhang above";

            return null;
        }

        public static void OnEnter(Player player, Ped ped, DroneState s)
        {
            // 1. Save originals (so we can restore even if interrupted).
            // We can't reliably read invincibility / ragdoll-allowed flags via
            // exposed natives, so we save what we can and restore to safe
            // defaults for the rest. This matches typical god-mode-off play.
            s.PlayerOriginal = ped.Position;
            s.HeadingOriginal = ped.Heading;
            s.PlayerInvincibleBefore = player.IsInvincible;
            s.PedInvincibleBefore = false;
            s.PedCanRagdollBefore = true;
            s.WantedMultiplierBefore = 1.0f;
            s.WantedLevelBefore = player.Wanted.WantedLevel;

            // 2. Apply flight-mode flags (spec A1, A2, A4).
            Natives.SetEntityCollision(ped, false, true);
            Natives.SetEntityInvincible(ped, true);
            Natives.SetPlayerInvincible(player, true);
            Natives.SetPedCanRagdoll(ped, false);
            Natives.SetEntityAlpha(ped, Config.PlayerAlpha, false);

            Natives.SetWantedLevelMultiplier(0.0f);

            // Initialise drone vector state.
            s.PlayerOriginal = ped.Position;
            s.Spawn = ped.Position;
            s.P = ped.Position + new Vector3(0, 0, Config.SpawnHeight);
            s.V = Vector3.Zero;
            s.Psi = ped.Heading * MathF.Deg2Rad;
            s.Theta = -0.3f;
            s.PhiCam = 0.0f;
            s.T = Config.TInitial;
            s.B = Config.BInitial;
            s.Q = 1.0f;
            s.I = 0.0f;
            s.Stage = FlightStage.Controlled;
            s.LoS = true;
            s.WallsCount = 0;
        }

        public static void OnExit(Player player, Ped ped, DroneState s)
        {
            // Always reset time scale (also done by SlowMoFinal finally — defensive).
            Game.TimeScale = 1.0f;

            // Stop any animpostfx we may have started.
            Natives.AnimpostfxStopAll();

            // Teleport player back to where they launched from.
            Natives.SetEntityCoordsNoOffset(ped, s.PlayerOriginal);
            Natives.SetEntityHeading(ped, s.HeadingOriginal);

            // Restore player flags.
            Natives.SetEntityCollision(ped, true, true);
            Natives.SetEntityInvincible(ped, s.PedInvincibleBefore);
            Natives.SetPlayerInvincible(player, s.PlayerInvincibleBefore);
            Natives.SetPedCanRagdoll(ped, s.PedCanRagdollBefore);
            Natives.ResetEntityAlpha(ped);

            Natives.SetWantedLevelMultiplier(s.WantedMultiplierBefore);

            // If anything bumped the wanted level above what the player started
            // with, roll it back. (Spec M5.)
            int after = Natives.GetWantedLevel(player);
            if (after > s.WantedLevelBefore)
                Natives.SetPlayerWantedLevel(player, s.WantedLevelBefore, false);
        }
    }

    internal static class MathF
    {
        public const float Pi = 3.14159265358979f;
        public const float Deg2Rad = Pi / 180.0f;
        public const float Rad2Deg = 180.0f / Pi;
    }
}
