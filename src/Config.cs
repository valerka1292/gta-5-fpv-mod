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

        // 4.2 Roll camera. Casual tuning: KRoll halved (10 → 4) and PhiMax
        // tightened so the screen doesn't visibly twist during fast yaws —
        // FPV-pilot eye candy that confused first-time fliers.
        public const float KRoll = 4.0f;
        public const float KRollResponse = 7.0f;
        public const float KRollRecovery = 6.0f;
        public const float PhiMax = 0.30f;

        // 4.4 Throttle
        public const float TMin = 0.0f;
        public const float TMax = 35.0f;            // m/s effective target speed
        public const float DTStep = 2.0f;           // T/Y step size
        public const float ThrottleRate = 12.0f;    // m/s per second while T/Y held
        public const float TInitial    = 0.0f;   // дрон стартует с нулевой скорости
        public const float TSpoolUp    = 20.0f;  // целевая скорость после раскрутки (м/с)
        public const float TSpoolDuration = 1.5f; // время раскрутки от 0 до TSpoolUp (сек реального времени)

        // 4.5 Vertical
        public const float VVertStep = 8.0f;        // PgUp/PgDn

        // 4.6 Gravity
        // Casual tuning: spec value 4.0 produced an obvious "sag" at any
        // throttle below max (g_eff = GWorld * (1 - T/TMax)). At 1.0 the drone
        // basically holds altitude as long as there is some throttle — much
        // more intuitive for a casual flier. Battery-dead path uses full GWorld.
        public const float GWorld = 1.0f;           // m/s²

        // 4.7-4.8 Inertia + drag
        // Casual tuning: KInertia 5 → 25 makes V chase V_target almost
        // instantly (≈ 40 ms time constant). The drone "snaps" to the camera
        // direction instead of drifting on its own momentum.
        public const float KInertia = 25.0f;
        public const float KInertiaDead = 1.0f;
        public const float CDrag = 0.020f;
        public const float CDragDead = 0.05f;

        // 6.1 Battery
        public const float KBaseDrain = 0.4f;       // %/s
        public const float KThrottleDrain = 0.7f;   // %/s
        public const float BCrit = 30.0f;           // % low-battery threshold
        public const float BInitial = 100.0f;
        public const float SinkMax = 4.0f;          // m/s

        // 7 Signal — city / field profiles. Values bumped from the v1.1 spec
        // baseline (audit #5): 900 m city max was too restrictive — by the
        // time you fly past a couple of blocks you hit lost signal, which
        // doesn't match player expectation for a casual fun mod. Field
        // values rescaled in proportion.
        public const float DSafeCity = 700.0f;
        public const float DCritCity = 1200.0f;
        public const float DMaxCity = 2000.0f;
        public const float DSafeField = 3000.0f;
        public const float DCritField = 5500.0f;
        public const float DMaxField = 9000.0f;
        public const float WallPenalty = 0.08f;     // -8% Q per wall
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
        // NOTE: ExplosionRadius is documentation-only right now; GTA's
        // ADD_EXPLOSION uses an internal preset radius based on explosionType.
        // Gameplay tuning uses VehicleImpulseRadius below.
        // ExplosionType.Grenade is type 2 in GTA native enum — see Collision.cs

        // 10 Slow-mo
        public const float TimeScaleSlow = 0.15f;
        public const float TimeScaleRamp = 0.1f;    // realtime
        public const float CamTransition = 0.08f;   // realtime fallback for camera move
        public const float CamHoldAfterExplosion = 2.0f;
        public const float DCam = 6.0f;
        public const float HCam = 2.0f;
        public const float CamMinClearance = 2.0f;
        // Extra distance added to the per-frame travel ray. Acts as the
        // "feeler" margin so very slow ramming into a wall still detonates,
        // and as an anti-tunneling safety on the high-speed end.
        public const float ContactMargin = 0.30f;
        // After contact, the drone is snapped to hit_point − F * SnapInset so
        // the FPV camera "kisses" the surface but doesn't penetrate it.
        public const float SnapInset = 0.05f;
        // Effective hit-volume radius of the drone for the multi-ray fan.
        // Larger → more reliable hits on thin geometry (car doors, glass)
        // at the cost of detonating slightly before the centre crosshair
        // visually touches.
        public const float DroneRadius = 0.25f;

        // Damage / impulse scaling vs. impact speed.
        //   speed = DamageScaleMinSpeed → damageScale = 1.0  (stock blast)
        //   speed = TMax                → damageScale = DamageScaleMax (boosted)
        public const float DamageScaleMinSpeed = 5.0f;
        public const float DamageScaleMax = 3.0f;
        // Range within which we apply a directional shock impulse to vehicles
        // post-detonation (audit #3 fix).
        public const float VehicleImpulseRadius = 8.0f;
        // Newton-seconds of impulse per (m/s of impact speed) per (1.0 - dist/R).
        // Tuned so a TMax kamikaze flips a Phantom truck.
        public const float VehicleImpulsePerSpeed = 4000.0f;
        public const float AutoLevelStrength = 0.0f;     // 0 = off (casual camera-aim flight)
        public const float AutoLevelInputDeadzone = 0.005f;

        // 12 HUD
        public const float KHorizonScale = 150.0f;  // px / rad

        // 13 Frame loop
        public const float DtClampMax = 0.05f;
        public const int FpvFov = 80;
        public const float SpawnHeight = 3.0f;
        public const int PlayerAlpha = 100;

        // Win32 raw-mouse capture: pixels of cursor delta = 1.0 normalized
        // input unit. Lower = more sensitive. 250 px is a forgiving default
        // tuned for casual play — first-time fliers who haven't pre-scaled
        // their mouse for a 0.08 rad/frame yaw factor don't get whipped
        // around. FPV pilots can drop this toward 100 if it feels sluggish.
        public const float MousePixelsPerUnit = 250.0f;
    }
}
