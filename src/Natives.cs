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

        public static void TaskShootAtCoord(Ped p, Vector3 pos, int durationMs, uint firingPattern)
        {
            Function.Call(Hash.TASK_SHOOT_AT_COORD,
                p.Handle, pos.X, pos.Y, pos.Z, durationMs, firingPattern);
        }

        public static void TaskReactAndFlee(Ped p, Ped target)
        {
            Function.Call(Hash.TASK_REACT_AND_FLEE_PED, p.Handle, target.Handle);
        }

        public static bool IsPedACop(Ped p)
        {
            PedType type = p.PedType;
            return type == PedType.Cop || type == PedType.Swat || type == PedType.Army;
        }

        public static Ped GetClosestPed(Vector3 position, float radius)
        {
            return GetClosestPedByType(position, radius, -1);
        }

        public static Ped GetClosestCop(Vector3 position, float radius)
        {
            return GetClosestPedByType(position, radius, (int)PedType.Cop);
        }

        private static Ped GetClosestPedByType(Vector3 position, float radius, int pedType)
        {
            var outPed = new OutputArgument();
            bool found = Function.Call<bool>(Hash.GET_CLOSEST_PED,
                position.X, position.Y, position.Z, radius,
                true, true, outPed, false, false, pedType);
            if (!found) return null;

            int handle = outPed.GetResult<int>();
            if (handle == 0) return null;
            return Entity.FromHandle(handle) as Ped;
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

        public static void SetSeethrough(bool toggle)
        {
            Function.Call(Hash.SET_SEETHROUGH, toggle);
        }

        public static void SetNightvision(bool toggle)
        {
            Function.Call(Hash.SET_NIGHTVISION, toggle);
        }

        public static void DisableAllControlsThisFrame()
        {
            // Block all three control groups for safety (spec M5).
            Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);
            Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 1);
            Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 2);
        }

        // Проверка, открыт ли телефон
        public static bool IsCellPhoneUp()
        {
            return Function.Call<bool>(Hash.IS_PED_RUNNING_MOBILE_PHONE_TASK, Game.Player.Character.Handle);
        }

        // Принудительное закрытие телефона
        public static void CloseCellPhone()
        {
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "cellphone_flashhand");
            Function.Call(Hash.TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME, "cellphone_controller");
        }

        public static float GetDisabledControlNormal(int group, int control)
        {
            return Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, group, control);
        }

        public static bool GetScreenCoordFromWorldCoord(Vector3 worldPos, out float screenX, out float screenY)
        {
            // GET_SCREEN_COORD_FROM_WORLD_COORD returns normalized 0..1 X/Y coordinates.
            var x = new OutputArgument();
            var y = new OutputArgument();
            bool visible = Function.Call<bool>(Hash.GET_SCREEN_COORD_FROM_WORLD_COORD,
                worldPos.X, worldPos.Y, worldPos.Z, x, y);
            screenX = x.GetResult<float>();
            screenY = y.GetResult<float>();
            return visible;
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

        public static void AddOwnedExplosion(Entity owner, Vector3 pos, int explosionType, float damageScale, bool audible, bool invisible, float cameraShake)
        {
            if (owner == null || !owner.Exists())
            {
                AddExplosionUnowned(pos, explosionType, damageScale, audible, invisible, cameraShake);
                return;
            }

            // ADD_OWNED_EXPLOSION attributes the blast to the owner entity.
            // This lets cops/civilians react to the player as the source.
            Function.Call(Hash.ADD_OWNED_EXPLOSION,
                owner.Handle,
                pos.X, pos.Y, pos.Z,
                explosionType, damageScale, audible, invisible, cameraShake);
        }

        // APPLY_FORCE_TO_ENTITY — Battlefield-style impulse on nearby
        // vehicles after detonation. forceType=1 = impulse-change-by-direction.
        //   force        — in WORLD space (isDirectionRel=false, isForceRel=false).
        //   localOffset  — point of application in the entity's LOCAL space
        //                  (computed via GetOffsetFromEntityGivenWorldCoords).
        //                  Non-zero offset is what gives the engine torque so
        //                  vehicles actually rotate/flip instead of just sliding.
        public static void ApplyForceToEntity(Entity e, Vector3 force, Vector3 localOffset, int forceType)
        {
            Function.Call(Hash.APPLY_FORCE_TO_ENTITY,
                e.Handle, forceType,
                force.X, force.Y, force.Z,
                localOffset.X, localOffset.Y, localOffset.Z,
                0,           // boneIndex
                false,       // isDirectionRel — force vector is world-space
                false,       // ignoreUpVec
                false,       // isForceRel — force vector is world-space
                false,       // p12
                true);       // p13
        }

        // GET_OFFSET_FROM_ENTITY_GIVEN_WORLD_COORDS — converts a world-space
        // point to the entity's local-space offset. Required for properly
        // applying torque-generating force at the impact point.
        public static Vector3 GetOffsetFromEntityGivenWorldCoords(Entity e, Vector3 worldPos)
        {
            return Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_GIVEN_WORLD_COORDS,
                e.Handle, worldPos.X, worldPos.Y, worldPos.Z);
        }
    }
}
