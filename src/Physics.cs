using System;
using GTA.Math;

namespace FpvDroneMod
{
    // Spec sections 4 (physics), 6 (battery).
    // Pure functions over DroneState; no engine calls.
    internal static class Physics
    {
        public static Vector3 ForwardFromYawPitch(float psi, float theta)
        {
            // F = (sin ψ cos θ, cos ψ cos θ, -sin θ)
            float sp = (float)Math.Sin(psi);
            float cp = (float)Math.Cos(psi);
            float st = (float)Math.Sin(theta);
            float ct = (float)Math.Cos(theta);
            return new Vector3(sp * ct, cp * ct, -st);
        }

        public static float Lerp(float a, float b, float k) => a + (b - a) * Clamp01(k);

        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public static float Clamp(float v, float lo, float hi)
            => v < lo ? lo : (v > hi ? hi : v);

        // 4.1 Yaw / pitch — frame-rate independent (multiplied by dt*60).
        public static void ApplyAngularInput(DroneState s, float dmx, float dmy, float dt)
        {
            float scale = dt * 60.0f;
            float psiRate = dmx * Config.SYaw * scale;
            s.Psi += psiRate;
            s.LastPsiRate = psiRate;

            float dTheta = -dmy * Config.SPitch * scale;
            s.Theta = Clamp(s.Theta + dTheta, -Config.ThetaMax, Config.ThetaMax);
        }

        // 4.2 Roll camera lerp.
        public static void UpdateCameraRoll(DroneState s, float dmx, float dt)
        {
            float target = -s.LastPsiRate * Config.KRoll;

            if (Math.Abs(dmx) < 0.01f)
            {
                // Recovery toward zero when no yaw input.
                s.PhiCam = s.PhiCam * (1.0f - Config.KRollRecovery * dt);
            }
            else
            {
                float k = Clamp01(Config.KRollResponse * dt);
                s.PhiCam = s.PhiCam + (target - s.PhiCam) * k;
            }
            s.PhiCam = Clamp(s.PhiCam, -Config.PhiMax, Config.PhiMax);
        }

        // 4.3-4.9 — Velocity / position integration.
        public static void IntegrateMotion(DroneState s, float vVert, float dt, bool batteryDead)
        {
            Vector3 F = ForwardFromYawPitch(s.Psi, s.Theta);

            // 6.3 V_sink when low battery (only if not fully dead — once dead T=0 anyway)
            Vector3 vSink = Vector3.Zero;
            if (!batteryDead && s.B < Config.BCrit)
            {
                float bLow = 1.0f - (s.B / Config.BCrit);
                float sinkRate = bLow * bLow * Config.SinkMax;
                vSink = new Vector3(0, 0, -sinkRate);
            }

            Vector3 vTarget = F * s.T + new Vector3(0, 0, vVert) + vSink;

            // 4.6 g_eff
            float gEff = batteryDead ? Config.GWorld : Config.GWorld * (1.0f - s.T / Config.TMax);
            Vector3 G = new Vector3(0, 0, -gEff);

            // 4.7 inertia
            float kInertia = batteryDead ? Config.KInertiaDead : Config.KInertia;
            Vector3 vPhys = Lerp(s.V, vTarget, Clamp01(kInertia * dt)) + G * dt;

            // 4.8 quadratic drag: F_drag = -V * C_drag * |V|
            float cDrag = batteryDead ? Config.CDragDead : Config.CDrag;
            float speed = vPhys.Length();
            Vector3 fDrag = -vPhys * cDrag * speed;

            s.V = vPhys + fDrag * dt;

            // 4.9 position
            s.P = s.P + s.V * dt;
        }

        public static Vector3 Lerp(Vector3 a, Vector3 b, float k)
        {
            return new Vector3(a.X + (b.X - a.X) * k, a.Y + (b.Y - a.Y) * k, a.Z + (b.Z - a.Z) * k);
        }

        // 6.1 Battery drain.
        public static void DrainBattery(DroneState s, float dt)
        {
            float dB = (Config.KBaseDrain + Config.KThrottleDrain * (s.T / Config.TMax)) * dt;
            s.B = Math.Max(s.B - dB, 0.0f);
        }

        // T/Y throttle
        public static void AdjustThrottle(DroneState s, float delta)
        {
            s.T = Clamp(s.T + delta, Config.TMin, Config.TMax);
        }
    }
}
