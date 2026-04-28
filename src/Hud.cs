using System;
using System.Drawing;
using GTA;
using GTA.Math;
using GTA.UI;

namespace FpvDroneMod
{
    // Spec section 12: FPV monochrome HUD overlay.
    // Drawn after RenderingCamera in OnTick so it sits on top.
    internal static class Hud
    {
        // Anchor positions are 1080p-relative; SHVDN UI scales them automatically.
        private const float ScreenW = 1920f;
        private const float ScreenH = 1080f;

        private static readonly Color White       = Color.FromArgb(220, 230, 230, 230);
        private static readonly Color Green       = Color.FromArgb(220, 80, 220, 80);
        private static readonly Color Yellow      = Color.FromArgb(220, 240, 200, 60);
        private static readonly Color Orange      = Color.FromArgb(220, 240, 140, 40);
        private static readonly Color Red         = Color.FromArgb(220, 230, 60, 60);
        private static readonly Color Cyan        = Color.FromArgb(220, 90, 180, 220);

        public static void Draw(DroneState s, float fpvFov)
        {
            // SPD (top-left)
            float speedKmh = s.V.Length() * 3.6f;
            Color spdColor = (s.V.Length() > 0.9f * Config.TMax) ? Red : White;
            DrawText("SPD",            new PointF(60,  60), 0.55f, White);
            DrawText($"{(int)speedKmh} km/h", new PointF(60,  90), 0.7f,  spdColor);

            // ALT (left-center)
            float groundZ = World.GetGroundHeight(s.P);
            float alt = s.P.Z - groundZ;
            Color altColor = (alt < 5f) ? Orange : White;
            DrawText("ALT",            new PointF(60, 480), 0.55f, White);
            DrawText($"{(int)alt}m",   new PointF(60, 510), 0.7f,  altColor);

            // Throttle bar (bottom-left)
            float throt = s.T / Config.TMax;
            Color thrColor = throt < 0.2f ? Cyan : (throt > 0.8f ? Yellow : White);
            DrawText($"THR: {(int)(throt * 100)}%", new PointF(60, 940), 0.55f, thrColor);
            DrawBar(new PointF(60, 970), new SizeF(180, 10), throt, thrColor);

            // Pitch (bottom-left, below throttle)
            float pitchDeg = s.Theta * MathF.Rad2Deg;
            Color pitchColor = (Math.Abs(pitchDeg) > 50f) ? Yellow : White;
            DrawText($"PITCH: {(int)pitchDeg}°", new PointF(60, 1000), 0.55f, pitchColor);

            // BAT (top-right)
            Color batColor;
            if (s.B > 50f) batColor = Green;
            else if (s.B > 25f) batColor = Yellow;
            else batColor = (Game.GameTime / 250 % 2 == 0) ? Red : White; // blink
            DrawText("BAT",                      new PointF(ScreenW - 220, 60), 0.55f, White);
            DrawText($"{(int)s.B}%",             new PointF(ScreenW - 220, 90), 0.7f, batColor);
            DrawBar(new PointF(ScreenW - 220, 130), new SizeF(180, 10), s.B / 100f, batColor);

            // DIST (bottom-right)
            float dist = (s.P - s.Spawn).Length();
            float dCrit = s.LoS ? Config.DCritField : Config.DCritCity;
            float dMax  = s.LoS ? Config.DMaxField  : Config.DMaxCity;
            Color distColor = dist >= dMax ? Red : (dist >= dCrit ? Yellow : White);
            DrawText($"DIST: {(int)dist}m",      new PointF(ScreenW - 280, 940), 0.55f, distColor);

            // SIG (bottom-right)
            int bars = (int)Math.Floor(s.Q * 4f);
            Color sigColor = s.Q < 0.30f ? Red : White;
            DrawText("SIG", new PointF(ScreenW - 280, 1000), 0.55f, sigColor);
            for (int i = 0; i < 4; i++)
            {
                Color c = i < bars ? sigColor : Color.FromArgb(80, 80, 80, 80);
                new ContainerElement(
                    new PointF(ScreenW - 220 + i * 14, 1000 + (4 - i) * 4),
                    new SizeF(10, 10 + i * 4), c).Draw();
            }

            // Status banner (top-center)
            DrawStatusBanner(s);

            // Horizon line (center)
            DrawHorizon(s);

            // Crosshair (center) — hidden in autonomous mode.
            if (s.Stage != FlightStage.Lost)
                DrawText("·", new PointF(ScreenW / 2 - 6, ScreenH / 2 - 18), 1.0f, White);
        }

        private static void DrawStatusBanner(DroneState s)
        {
            string text = "[ARMED]"; Color c = Green;
            if (s.B == 0f && s.MotorsDeadBannerT > 0f) { text = "[MOTORS DEAD]"; c = Red; }
            else if (s.ImpactImminent) { text = "[IMPACT IMMINENT]"; c = Red; }
            else if (s.Stage == FlightStage.Lost) { text = "[AUTONOMOUS MODE]"; c = Red; }
            else if (s.Q < 0.15f) { text = "[SIGNAL LOST]"; c = Red; }
            else if (s.SignalRestoredBannerT > 0f) { text = "[SIGNAL RESTORED]"; c = Green; }
            else if (s.B < 10f) { text = "[CRITICAL BAT]"; c = Red; }
            else if (s.B < 25f) { text = "[LOW BAT]"; c = Yellow; }

            DrawText(text, new PointF(ScreenW / 2 - 100, 60), 0.7f, c);
        }

        private static void DrawHorizon(DroneState s)
        {
            float yOff = s.Theta * Config.KHorizonScale; // pitch shifts horizon
            float yCenter = ScreenH / 2 + yOff;
            float angleRad = -s.PhiCam;

            // 600 px horizon line — drawn via a thin rotated rectangle approximation
            // by stacking small segments. SHVDN doesn't expose rotated rectangles
            // directly, so we approximate by stepping pixels.
            const int segments = 30;
            const float halfLen = 280f;
            float dx = (float)Math.Cos(angleRad) * (halfLen * 2 / segments);
            float dy = (float)Math.Sin(angleRad) * (halfLen * 2 / segments);
            float x0 = ScreenW / 2 - (float)Math.Cos(angleRad) * halfLen;
            float y0 = yCenter - (float)Math.Sin(angleRad) * halfLen;

            for (int i = 0; i < segments; i++)
            {
                new ContainerElement(
                    new PointF(x0 + dx * i, y0 + dy * i),
                    new SizeF(8, 2), White).Draw();
            }
        }

        private static void DrawText(string text, PointF pos, float scale, Color c)
        {
            new TextElement(text, pos, scale, c, GTA.UI.Font.ChaletLondon).Draw();
        }

        private static void DrawBar(PointF pos, SizeF size, float fill01, Color c)
        {
            // Border (semi-transparent)
            new ContainerElement(pos, size, Color.FromArgb(60, 200, 200, 200)).Draw();
            float w = Math.Max(0f, Math.Min(1f, fill01)) * size.Width;
            new ContainerElement(pos, new SizeF(w, size.Height), c).Draw();
        }
    }
}
