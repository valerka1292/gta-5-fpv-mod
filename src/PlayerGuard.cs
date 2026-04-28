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
            // Hide ped fully — alpha=100 leaves the FPV camera (which sits at
            // the ped's coords) seeing the legs/arms from inside the model.
            // Trade-off: NPC/cops can no longer see the player either, so the
            // "under-fire ghost" atmosphere from the spec is weaker. User
            // explicitly preferred not seeing their own legs.
            ped.IsVisible = false;
            Natives.SetEntityAlpha(ped, Config.PlayerAlpha, false);

            Natives.SetWantedLevelMultiplier(0.0f);

            // Initialise drone vector state.
            s.PlayerOriginal = ped.Position;
            s.Spawn = ped.Position;
            s.P = ped.Position + new Vector3(0, 0, Config.SpawnHeight);
            s.V = Vector3.Zero;
            // Spawn drone aligned with the player's CAMERA, not their body
            // heading. The player's torso may be facing one direction while
            // the camera looks somewhere else — and the drone should fly
            // toward where the player is *looking*. (Audit #7 fix.)
            //
            // GTA heading convention is counter-clockwise from +Y north;
            // GameplayCamera.Rotation.Z is the same convention with the
            // same sign, so we just convert deg→rad.
            //
            // Pitch (Theta): GTA Camera.Rotation.X is positive when looking
            // up, negative when looking down — same sign convention as our
            // drone Theta where Theta>0 means F.z>0 (nose up). So no
            // sign flip needed here.
            float camYawDeg = GameplayCamera.Rotation.Z;
            float camPitchDeg = GameplayCamera.Rotation.X;
            s.Psi = camYawDeg * MathF.Deg2Rad;
            s.Theta = camPitchDeg * MathF.Deg2Rad;
            // Clamp initial pitch into the configured cinematic range so a
            // player aiming straight up/down doesn't break the integrator.
            if (s.Theta < -1.4f) s.Theta = -1.4f;
            if (s.Theta >  1.4f) s.Theta =  1.4f;
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
            ped.IsVisible = true;
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
