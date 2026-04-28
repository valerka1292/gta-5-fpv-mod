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
        // SHVDN3's UI library uses GTA.UI.Screen.Width × Screen.Height as the
        // base coordinate system (a 720-px-height-based virtual canvas, NOT
        // the physical resolution). Reading them at draw time keeps everything
        // centred regardless of the user's actual display.
        private static float ScreenW => GTA.UI.Screen.Width;
        private static float ScreenH => GTA.UI.Screen.Height;

        private static readonly Color White       = Color.FromArgb(220, 230, 230, 230);
        private static readonly Color Green       = Color.FromArgb(220, 80, 220, 80);
        private static readonly Color Yellow      = Color.FromArgb(220, 240, 200, 60);
        private static readonly Color Orange      = Color.FromArgb(220, 240, 140, 40);
        private static readonly Color Red         = Color.FromArgb(220, 230, 60, 60);
        private static readonly Color Cyan        = Color.FromArgb(220, 90, 180, 220);

        public static void Draw(DroneState s, float fpvFov)
        {
            // All HUD positions are computed against the live UI canvas
            // (Screen.Width × Screen.Height) so resolution / aspect ratio
            // changes don't push elements off-centre. Margins are expressed
            // as small absolute pixels in the 720-base coordinate space.
            float W = ScreenW, H = ScreenH;
            const float marginX = 40f;   // distance from left/right edges
            const float topY    = 30f;   // top text row
            const float botY1   = 72f;   // bottom row 1, measured from bottom up
            const float botY2   = 42f;   // bottom row 2, ditto

            // SPD (top-left)
            float speedKmh = s.V.Length() * 3.6f;
            Color spdColor = (s.V.Length() > 0.9f * Config.TMax) ? Red : White;
            DrawText("SPD",                   new PointF(marginX, topY),       0.4f, White);
            DrawText($"{(int)speedKmh} km/h", new PointF(marginX, topY + 22f), 0.55f, spdColor);

            // ALT (left, mid-screen)
            float groundZ = 0f;
            if (!World.GetGroundHeight(s.P, out groundZ, GetGroundHeightMode.Normal))
                groundZ = s.P.Z;
            float alt = s.P.Z - groundZ;
            Color altColor = (alt < 5f) ? Orange : White;
            DrawText("ALT",          new PointF(marginX, H * 0.45f),       0.4f, White);
            DrawText($"{(int)alt}m", new PointF(marginX, H * 0.45f + 22f), 0.55f, altColor);

            // Throttle (bottom-left)
            float throt = s.T / Config.TMax;
            Color thrColor = throt < 0.2f ? Cyan : (throt > 0.8f ? Yellow : White);
            DrawText($"THR: {(int)(throt * 100)}%", new PointF(marginX, H - botY1), 0.4f, thrColor);
            DrawBar(new PointF(marginX, H - botY1 + 18f), new SizeF(120, 6), throt, thrColor);

            // Pitch (bottom-left, below throttle)
            float pitchDeg = s.Theta * MathF.Rad2Deg;
            Color pitchColor = (Math.Abs(pitchDeg) > 50f) ? Yellow : White;
            DrawText($"PITCH: {(int)pitchDeg}°", new PointF(marginX, H - botY2), 0.4f, pitchColor);

            // BAT (top-right). Text is right-aligned by offsetting the
            // anchor by an estimated width — at 0.4 / 0.55 scale the text is
            // ~120 px wide.
            Color batColor;
            if (s.B > 50f) batColor = Green;
            else if (s.B > 25f) batColor = Yellow;
            else batColor = (Game.GameTime / 250 % 2 == 0) ? Red : White; // blink
            DrawText("BAT",          new PointF(W - marginX - 140f, topY),       0.4f, White);
            DrawText($"{(int)s.B}%", new PointF(W - marginX - 140f, topY + 22f), 0.55f, batColor);
            DrawBar(new PointF(W - marginX - 140f, topY + 50f), new SizeF(140, 6), s.B / 100f, batColor);

            // DIST (bottom-right)
            float dist = (s.P - s.Spawn).Length();
            float dCrit = s.LoS ? Config.DCritField : Config.DCritCity;
            float dMax  = s.LoS ? Config.DMaxField  : Config.DMaxCity;
            Color distColor = dist >= dMax ? Red : (dist >= dCrit ? Yellow : White);
            DrawText($"DIST: {(int)dist}m", new PointF(W - marginX - 140f, H - botY1), 0.4f, distColor);

            // SIG bars (bottom-right)
            int bars = (int)Math.Floor(s.Q * 4f);
            Color sigColor = s.Q < 0.30f ? Red : White;
            DrawText("SIG", new PointF(W - marginX - 140f, H - botY2), 0.4f, sigColor);
            for (int i = 0; i < 4; i++)
            {
                Color c = i < bars ? sigColor : Color.FromArgb(80, 80, 80, 80);
                new ContainerElement(
                    new PointF(W - marginX - 100f + i * 9f, H - botY2 + (4 - i) * 2f),
                    new SizeF(7, 7 + i * 2f), c).Draw();
            }

            // Status banner (top-center)
            DrawStatusBanner(s);

            // Horizon line (center)
            DrawHorizon(s);

            // FPV crosshair (center) — hidden in autonomous mode (Lost stage).
            if (s.Stage != FlightStage.Lost)
                DrawCrosshair(s);
        }

        // FPV-style "+" crosshair: four arms with a small center gap and a
        // single center pip. Casual mode — velocity vector indicator removed
        // since the drone now slaves V to camera direction instantly anyway.
        private static void DrawCrosshair(DroneState s)
        {
            float cx = ScreenW / 2f;
            float cy = ScreenH / 2f;
            // Sized for the 720-base canvas — small but readable.
            const float arm = 12f;
            const float gap = 4f;
            const float thick = 1.5f;
            new ContainerElement(new PointF(cx - gap - arm, cy - thick / 2f),
                                 new SizeF(arm, thick), White).Draw();
            new ContainerElement(new PointF(cx + gap, cy - thick / 2f),
                                 new SizeF(arm, thick), White).Draw();
            new ContainerElement(new PointF(cx - thick / 2f, cy - gap - arm),
                                 new SizeF(thick, arm), White).Draw();
            new ContainerElement(new PointF(cx - thick / 2f, cy + gap),
                                 new SizeF(thick, arm), White).Draw();
            new ContainerElement(new PointF(cx - 1.5f, cy - 1.5f),
                                 new SizeF(3, 3), White).Draw();
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

            DrawText(text, new PointF(ScreenW / 2f - 70f, 30f), 0.5f, c);
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
            const float halfLen = 180f;
            float dx = (float)Math.Cos(angleRad) * (halfLen * 2 / segments);
            float dy = (float)Math.Sin(angleRad) * (halfLen * 2 / segments);
            float x0 = ScreenW / 2 - (float)Math.Cos(angleRad) * halfLen;
            float y0 = yCenter - (float)Math.Sin(angleRad) * halfLen;

            for (int i = 0; i < segments; i++)
            {
                new ContainerElement(
                    new PointF(x0 + dx * i, y0 + dy * i),
                    new SizeF(5, 1), White).Draw();
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
