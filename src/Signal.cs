using System;
using GTA;
using GTA.Math;

namespace FpvDroneMod
{
    // Spec section 7. Signal quality / interference.
    internal static class Signal
    {
        // Cached LoS — refreshed every Config.LosCheckInterval s realtime by the caller.
        public static void RefreshLineOfSight(DroneState s, Ped ignorePed)
        {
            // Single ray from spawn to drone. walls_count is approximated
            // by re-tracing while skipping previous hits (see CountWalls).
            var ray = World.Raycast(s.Spawn, s.P,
                                    IntersectFlags.Map,
                                    ignorePed);
            s.LoS = !ray.DidHit;
            s.WallsCount = CountWalls(s.Spawn, s.P, ignorePed);
        }

        // Approximate the number of geometry intersections by stepping along
        // the ray and counting transitions inside/outside hits. We cap at 6
        // because beyond that the signal is already fully dead and accuracy
        // doesn't matter.
        private static int CountWalls(Vector3 a, Vector3 b, Ped ignore)
        {
            const int maxWalls = 6;
            int walls = 0;
            Vector3 from = a;
            for (int i = 0; i < maxWalls + 1; i++)
            {
                var ray = World.Raycast(from, b, IntersectFlags.Map, ignore);
                if (!ray.DidHit) break;
                walls++;
                Vector3 dir = (b - from);
                float len = dir.Length();
                if (len < 0.01f) break;
                dir = dir / len;
                from = ray.HitPosition + dir * 0.5f; // step past the hit
                if ((b - from).Length() < 0.5f) break;
            }
            return walls;
        }

        // 7.3 Compute Q and I.
        public static void ComputeQuality(DroneState s)
        {
            float dSafe = s.LoS ? Config.DSafeField : Config.DSafeCity;
            float dCrit = s.LoS ? Config.DCritField : Config.DCritCity;
            float dMax  = s.LoS ? Config.DMaxField  : Config.DMaxCity;

            float d = (s.P - s.Spawn).Length();

            float qDist;
            if (d <= dSafe) qDist = 1.0f;
            else if (d <= dCrit) qDist = 1.0f - ((d - dSafe) / (dCrit - dSafe)) * 0.6f;
            else if (d <= dMax) qDist = 0.4f - ((d - dCrit) / (dMax - dCrit)) * 0.4f;
            else qDist = 0.0f;

            float qWalls = Math.Max(1.0f - s.WallsCount * Config.WallPenalty, 0.0f);

            float qBattery;
            if (s.B >= 100.0f) qBattery = 1.0f;
            else if (s.B <= Config.BCrit) qBattery = 0.0f;
            else qBattery = (s.B - Config.BCrit) / (100.0f - Config.BCrit);

            float q = qDist * qWalls * qBattery;
            if (q < 0f) q = 0f; if (q > 1f) q = 1f;

            s.Q = q;
            s.I = 1.0f - q;
        }

        // 8.1-8.4 Stage transitions, using realtime dt for timers (spec B6).
        public static void UpdateStage(DroneState s, float dtReal)
        {
            switch (s.Stage)
            {
                case FlightStage.Controlled:
                case FlightStage.Degraded:
                    if (s.I >= Config.ILost)
                    {
                        s.LostTimerRealtime += dtReal;
                        if (s.LostTimerRealtime >= Config.LostHoldTime)
                        {
                            s.Stage = FlightStage.Lost;
                            s.LostTimerRealtime = 0;
                            // Lock heading on entry: F_last_known = F now
                            s.FLastKnown = Physics.ForwardFromYawPitch(s.Psi, s.Theta);
                            s.TLast = s.T;
                        }
                    }
                    else
                    {
                        s.LostTimerRealtime = 0;
                        s.Stage = (s.I >= Config.IDegraded)
                            ? FlightStage.Degraded
                            : FlightStage.Controlled;
                    }
                    break;

                case FlightStage.Lost:
                    if (s.I < Config.IRecover)
                    {
                        s.RecoverTimerRealtime += dtReal;
                        if (s.RecoverTimerRealtime >= Config.RecoverHoldTime)
                        {
                            s.Stage = FlightStage.Degraded;
                            s.RecoverTimerRealtime = 0;
                            s.LostTimerRealtime = 0;
                            s.SignalRestoredBannerT = 2.0f;
                        }
                    }
                    else
                    {
                        s.RecoverTimerRealtime = 0;
                    }
                    // Throttle ramps to TMax in autonomous mode (spec 8.3).
                    s.T = s.T + (Config.TMax - s.T) * Math.Min(1.0f, Config.LostThrottleRamp * dtReal);
                    // Yaw/pitch frozen — caller sets dmx=dmy=0 before calling Apply* funcs.
                    break;
            }
        }
    }
}
