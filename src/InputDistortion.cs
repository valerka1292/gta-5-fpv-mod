using System;

namespace FpvDroneMod
{
    // Spec section 8.2: Degraded-stage input distortion (drop + noise).
    // Single shared RNG keeps draws inexpensive and deterministic-ish.
    internal static class InputDistortion
    {
        private static readonly Random Rng = new Random();

        public static (float dmx, float dmy) Apply(float dmx, float dmy, float I, FlightStage stage)
        {
            if (stage == FlightStage.Lost)
                return (0.0f, 0.0f);
            if (stage != FlightStage.Degraded)
                return (dmx, dmy);

            // Drop chance = ((I - 0.50) / 0.35) × 0.40 (clamped).
            float t = (I - Config.IDegraded) / (Config.ILost - Config.IDegraded);
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            float dropChance = t * Config.MaxDropChance;

            if (Rng.NextDouble() < dropChance)
            {
                float kx = (float)Rng.NextDouble() * 0.3f;
                float ky = (float)Rng.NextDouble() * 0.3f;
                dmx *= kx;
                dmy *= ky;
            }

            // Noise injection.
            if (Rng.NextDouble() < dropChance * 0.3f)
            {
                dmx += ((float)Rng.NextDouble() - 0.5f) * 0.4f;
                dmy += ((float)Rng.NextDouble() - 0.5f) * 0.2f;
            }

            return (dmx, dmy);
        }

        // Frame-rate-correlated input delay is handled coarsely: we keep a tiny
        // ring buffer of recent samples and look up older ones. This is good
        // enough for the FPV "input lag" feel without doing full event queues.
        private const int BufLen = 32;
        private static readonly (float dmx, float dmy, float t)[] Buf = new (float, float, float)[BufLen];
        private static int Head;
        private static float Now;

        public static (float dmx, float dmy) ApplyDelay(float dmx, float dmy, float I, FlightStage stage, float dtReal)
        {
            Now += dtReal;
            Buf[Head] = (dmx, dmy, Now);
            Head = (Head + 1) % BufLen;

            if (stage != FlightStage.Degraded) return (dmx, dmy);

            float t = (I - Config.IDegraded) / (Config.ILost - Config.IDegraded);
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            float delaySec = (t * Config.MaxInputDelayMs) * 0.001f;

            float target = Now - delaySec;
            // Walk back from Head-1 to find sample with timestamp closest <= target.
            for (int i = 1; i <= BufLen; i++)
            {
                int idx = (Head - i + BufLen) % BufLen;
                if (Buf[idx].t <= target) return (Buf[idx].dmx, Buf[idx].dmy);
            }
            return (dmx, dmy);
        }

        public static void Reset()
        {
            for (int i = 0; i < BufLen; i++) Buf[i] = (0, 0, 0);
            Head = 0;
            Now = 0;
        }
    }
}
