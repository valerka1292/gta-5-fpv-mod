using GTA.Math;

namespace FpvDroneMod
{
    internal enum FlightStage
    {
        Controlled,
        Degraded,
        Lost
    }

    internal enum VisionMode
    {
        Normal,
        Thermal,
        NightVision
    }

    // Vector state from spec section 3.
    internal sealed class DroneState
    {
        public Vector3 P;            // drone position
        public Vector3 V;            // drone velocity
        public Vector3 F = new Vector3(0, 1, 0); // forward, refreshed each tick from cam.ForwardVector
        public float Psi;            // yaw, rad
        public float Theta;          // pitch, rad
        public float PhiCam;         // camera roll, rad
        public float T;              // throttle target speed, m/s
        public float B;              // battery, %
        public float Q;              // signal quality 0..1
        public float I;              // interference 0..1 = 1-Q
        public bool LoS;             // line of sight to spawn
        public int WallsCount;       // number of walls between spawn and drone
        public FlightStage Stage = FlightStage.Controlled;
        public VisionMode Vision = VisionMode.Normal;

        public Vector3 Spawn;        // P_spawn

        // Saved player state (restored on exit)
        public Vector3 PlayerOriginal;
        public float HeadingOriginal;
        public bool PlayerInvincibleBefore;
        public bool PedInvincibleBefore;
        public bool PedCanRagdollBefore;
        public float WantedMultiplierBefore;
        public int WantedLevelBefore;

        // Flight-loop helpers
        public Vector3 FLastKnown = Vector3.RelativeFront;
        public float TLast = 20.0f;
        public float LostTimerRealtime;       // s
        public float RecoverTimerRealtime;    // s
        public float LosCheckTimer;           // s — when ≥ LosCheckInterval, refresh
        public float SignalRestoredBannerT;   // s — countdown for banner
        public float MotorsDeadBannerT;       // s

        // Last computed inputs (for distortion display)
        public float LastDmx;
        public float LastDmy;
        public float LastPsiRate;

        // Раскрутка при старте
        public bool  Spooling      = false;
        public float SpoolElapsed  = 0f;    // сек реального времени
        public float FlightTimerReal = 0f;   // секунды с момента запуска
        public float MahUsed         = 0f;   // израсходованная ёмкость, мАч

        // NPC-реакция
        public float NpcHoverTimer    = 0f;  // сек — сколько дрон висит над пешеходом
        public float CopShootTimer    = 0f;  // сек — таймер кадения стрельбы копов
        public float PanicCooldown    = 0f;  // сек — антиспам паники пешеходов

        // Slow-mo banner flag (set by Main when Collision.Outcome.ImpactImminent fires)
        public bool ImpactImminent;
    }
}
