using GTA;
using GTA.Math;
using GTA.Native;

namespace FpvDroneMod
{
    // Thin wrappers for natives we use that aren't covered by the SHVDN3 wrapper
    // surface in a convenient way. Keeps Main.cs readable.
    internal static class Natives
    {
        public static void SetEntityCoordsNoOffset(Entity e, Vector3 pos)
        {
            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, e.Handle,
                pos.X, pos.Y, pos.Z, false, false, false);
        }

        public static void SetEntityHeading(Entity e, float headingDeg)
        {
            Function.Call(Hash.SET_ENTITY_HEADING, e.Handle, headingDeg);
        }

        public static void SetEntityCollision(Entity e, bool toggle, bool keepPhysics)
        {
            Function.Call(Hash.SET_ENTITY_COLLISION, e.Handle, toggle, keepPhysics);
        }

        public static void SetEntityInvincible(Entity e, bool toggle)
        {
            Function.Call(Hash.SET_ENTITY_INVINCIBLE, e.Handle, toggle);
        }

        public static void SetEntityAlpha(Entity e, int alpha, bool skin)
        {
            Function.Call(Hash.SET_ENTITY_ALPHA, e.Handle, alpha, skin);
        }

        public static void ResetEntityAlpha(Entity e)
        {
            Function.Call(Hash.RESET_ENTITY_ALPHA, e.Handle);
        }

        public static void SetPedCanRagdoll(Ped p, bool toggle)
        {
            Function.Call(Hash.SET_PED_CAN_RAGDOLL, p.Handle, toggle);
        }

        public static void SetPlayerInvincible(Player pl, bool toggle)
        {
            Function.Call(Hash.SET_PLAYER_INVINCIBLE, pl.Handle, toggle);
        }

        public static void SetWantedLevelMultiplier(float v)
        {
            Function.Call(Hash.SET_WANTED_LEVEL_MULTIPLIER, v);
        }

        public static void ClearPlayerWantedLevel(Player pl)
        {
            Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, pl.Handle);
        }

        public static void SetPlayerWantedLevel(Player pl, int level, bool delayedResponse)
        {
            Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, pl.Handle, level, delayedResponse);
            Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, pl.Handle, false);
        }

        public static int GetWantedLevel(Player pl)
        {
            return Function.Call<int>(Hash.GET_PLAYER_WANTED_LEVEL, pl.Handle);
        }

        public static void SetRadarAsExteriorThisFrame()
        {
            Function.Call(Hash.SET_RADAR_AS_EXTERIOR_THIS_FRAME);
        }

        public static bool IsCutscenePlaying()
        {
            return Function.Call<bool>(Hash.IS_CUTSCENE_PLAYING);
        }

        public static bool IsPedSwimming(Ped p)
        {
            return Function.Call<bool>(Hash.IS_PED_SWIMMING, p.Handle);
        }

        public static bool IsPedRagdoll(Ped p)
        {
            return Function.Call<bool>(Hash.IS_PED_RAGDOLL, p.Handle);
        }

        public static bool IsPauseMenuActive()
        {
            return Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE);
        }

        public static void DisableAllControlsThisFrame()
        {
            // Block all three control groups for safety (spec M5).
            Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);
            Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 1);
            Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 2);
        }

        public static float GetDisabledControlNormal(int group, int control)
        {
            return Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, group, control);
        }

        // 11.1 ANIMPOSTFX — discrete on/off (M2). No intensity native.
        public static void AnimpostfxPlay(string name, int duration, bool looped)
        {
            Function.Call(Hash.ANIMPOSTFX_PLAY, name, duration, looped);
        }

        public static void AnimpostfxStop(string name)
        {
            Function.Call(Hash.ANIMPOSTFX_STOP, name);
        }

        public static void AnimpostfxStopAll()
        {
            Function.Call(Hash.ANIMPOSTFX_STOP_ALL);
        }

        public static bool AnimpostfxIsRunning(string name)
        {
            return Function.Call<bool>(Hash.ANIMPOSTFX_IS_RUNNING, name);
        }

        public static void AddExplosionUnowned(Vector3 pos, int explosionType, float damageScale, bool audible, bool invisible, float cameraShake)
        {
            // ADD_EXPLOSION (no owner) — doesn't attribute to any ped, so no
            // wanted level applied to the player. (Spec A4.)
            Function.Call(Hash.ADD_EXPLOSION,
                pos.X, pos.Y, pos.Z,
                explosionType, damageScale, audible, invisible, cameraShake);
        }

        // APPLY_FORCE_TO_ENTITY — used to add a Battlefield-style impulse to
        // nearby vehicles after detonation so trucks actually flip on a
        // high-speed kamikaze hit, even with a low-power explosion type.
        // forceType=1 = impulse-change-relative-to-direction.
        public static void ApplyForceToEntity(Entity e, Vector3 force, Vector3 offset, int forceType)
        {
            Function.Call(Hash.APPLY_FORCE_TO_ENTITY,
                e.Handle, forceType,
                force.X, force.Y, force.Z,
                offset.X, offset.Y, offset.Z,
                0,           // boneIndex
                false,       // isDirectionRel
                true,        // ignoreUpVec
                true,        // isForceRel
                false,       // p12
                true);       // p13
        }
    }
}
