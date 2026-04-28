// Parameter table from spec section 14.
// All values are tunable; comments show the section that uses them.

namespace FpvDroneMod
{
    internal static class Config
    {
        // 4.1 Angles
        public const float SYaw = 0.08f;            // mouse → yaw rate (rad / per (dt*60))
        public const float SPitch = 0.06f;          // mouse → pitch rate
        public const float ThetaMax = 1.2f;         // pitch clamp (rad)

        // 4.2 Roll camera
        public const float KRoll = 10.0f;
        public const float KRollResponse = 7.0f;
        public const float KRollRecovery = 5.0f;
        public const float PhiMax = 0.52f;

        // 4.4 Throttle
        public const float TMin = 0.0f;
        public const float TMax = 35.0f;            // m/s effective target speed
        public const float DTStep = 2.0f;           // T/Y step size
        public const float TInitial = 20.0f;

        // 4.5 Vertical
        public const float VVertStep = 8.0f;        // PgUp/PgDn

        // 4.6 Gravity
        public const float GWorld = 4.0f;           // m/s²

        // 4.7-4.8 Inertia + drag
        public const float KInertia = 5.0f;
        public const float KInertiaDead = 1.0f;
        public const float CDrag = 0.020f;
        public const float CDragDead = 0.05f;

        // 6.1 Battery
        public const float KBaseDrain = 0.4f;       // %/s
        public const float KThrottleDrain = 0.7f;   // %/s
        public const float BCrit = 30.0f;           // % low-battery threshold
        public const float BInitial = 100.0f;
        public const float SinkMax = 4.0f;          // m/s

        // 7 Signal — city / field profiles
        public const float DSafeCity = 400.0f;
        public const float DCritCity = 650.0f;
        public const float DMaxCity = 900.0f;
        public const float DSafeField = 2000.0f;
        public const float DCritField = 4000.0f;
        public const float DMaxField = 7000.0f;
        public const float WallPenalty = 0.15f;     // -15% Q per wall
        public const float LosCheckInterval = 0.5f; // s realtime
        public const float EmergencyOverDMax = 200.0f;

        // 8 Degradation thresholds
        public const float IDegraded = 0.50f;
        public const float ILost = 0.85f;
        public const float LostHoldTime = 0.5f;     // realtime
        public const float MaxInputDelayMs = 200.0f;
        public const float MaxDropChance = 0.40f;
        public const float IRecover = 0.70f;
        public const float RecoverHoldTime = 1.0f;  // realtime
        public const float LostThrottleRamp = 1.5f; // lerp k for T → TMax in autonomous

        // 9 Collision / explosion
        public const float TPredict = 0.35f;        // realtime threshold for slow-mo
        public const float ExplosionRadius = 5.0f;  // informational
        // ExplosionType.Grenade is type 2 in GTA native enum — see Collision.cs

        // 10 Slow-mo
        public const float TimeScaleSlow = 0.15f;
        public const float TimeScaleRamp = 0.1f;    // realtime
        public const float CamTransition = 0.08f;   // realtime
        public const float CamHoldAfterExplosion = 2.0f;
        public const float DCam = 6.0f;
        public const float HCam = 2.0f;
        public const float CamMinClearance = 2.0f;

        // 12 HUD
        public const float KHorizonScale = 150.0f;  // px / rad

        // 13 Frame loop
        public const float DtClampMax = 0.05f;
        public const int FpvFov = 80;
        public const float SpawnHeight = 3.0f;
        public const int PlayerAlpha = 100;
    }
}
