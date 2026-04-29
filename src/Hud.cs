using System;
using System.Drawing;
using GTA;
using GTA.Math;
using GTA.UI;

namespace FpvDroneMod
{
    internal static class Hud
    {
        private static float ScreenW => GTA.UI.Screen.Width;
        private static float ScreenH => GTA.UI.Screen.Height;

        private static readonly Color White  = Color.FromArgb(255, 245, 245, 245);
        private static readonly Color Red    = Color.FromArgb(255, 230, 60, 60);
        private static readonly Color Dim    = Color.FromArgb(255, 180, 180, 180);

        private const float ROW_H = 18f;
        private const float TAPE_ROWS = 4f;
        private const float SPD_STEP = 10f;
        private const float ALT_STEP = 10f;

        public static void Draw(DroneState s, float fpvFov)
        {
            DrawTopRow(s);
            DrawCompassTape(s);
            DrawSpeedTape(s);
            DrawAltitudeTape(s);

            if (s.Stage != FlightStage.Lost)
                DrawCenterReticle();

            DrawStatusBanner(s);
            DrawHorizon(s);
            DrawBottomLeftBatteryBlock(s);
            DrawBottomRightInfo(s);
        }

        private static void DrawTopRow(DroneState s)
        {
            float topY = 25f;

            // Left (GPS + Home)
            DrawText($"LAT {s.P.Y,8:F4}", new PointF(30f, topY), 0.32f, White);
            DrawText($"LON {s.P.X,8:F4}", new PointF(30f, topY + 18f), 0.32f, White);

            float distHome = (s.P - s.Spawn).Length();
            string homeStr = distHome >= 1000f ? $"HOME {distHome / 1000f:F1}k" : $"HOME {(int)distHome}m";
            DrawText(homeStr, new PointF(30f, topY + 36f), 0.32f, White);

            // Right (RSSI) - Moved down to Y=60 to avoid wanted level stars
            int rssi = (int)Math.Round(Math.Max(0f, Math.Min(1f, s.Q)) * 100f);
            Color rssiColor = rssi < 30 ? Red : White;
            DrawText($"RSSI {rssi}%", new PointF(ScreenW - 100f, 60f), 0.32f, rssiColor);
        }

        private static void DrawCompassTape(DroneState s)
        {
            float cx = ScreenW * 0.5f;
            float y = 25f;
            float headingDeg = Normalize360(s.Psi * MathF.Rad2Deg);

            const float pxPerDeg = 3.5f;
            const float halfW = 150f;

            DrawOsdLine(new PointF(cx - halfW, y), new SizeF(halfW * 2f, 2f), White);

            for (int d = -45; d <= 45; d += 5)
            {
                float x = cx + d * pxPerDeg;
                bool major = d % 15 == 0;
                float h = major ? 10f : 5f;

                DrawOsdLine(new PointF(x - 1f, y - h), new SizeF(2f, h), White);

                if (major)
                {
                    int mark = (int)Normalize360(headingDeg + d);
                    DrawText(mark.ToString("000"), new PointF(x - 12f, y - 24f), 0.28f, White);
                }
            }

            DrawText("V", new PointF(cx - 5f, y + 2f), 0.30f, White);
            DrawText($"{(int)headingDeg:000}", new PointF(cx - 14f, y + 16f), 0.34f, White);
        }

        private static void DrawSpeedTape(DroneState s)
        {
            float x = 60f;
            float cy = ScreenH * 0.5f;
            float speed = s.V.Length() * 3.6f;
            int baseVal = (int)Math.Round(speed / SPD_STEP) * (int)SPD_STEP;

            for (int i = -(int)TAPE_ROWS; i <= (int)TAPE_ROWS; i++)
            {
                int val = baseVal + i * (int)SPD_STEP;
                if (val < 0) continue;

                float frac = (speed - baseVal) / SPD_STEP;
                float y = cy - (i - frac) * ROW_H;
                if (Math.Abs(y - cy) < 12f) continue;
                if (y < cy - TAPE_ROWS * ROW_H || y > cy + TAPE_ROWS * ROW_H) continue;

                DrawOsdLine(new PointF(x - 8f, y), new SizeF(8f, 2f), White);
                DrawText(val.ToString(), new PointF(x - 36f, y - 10f), 0.30f, White);
            }

            DrawOsdLine(new PointF(x, cy - TAPE_ROWS * ROW_H), new SizeF(2f, TAPE_ROWS * ROW_H * 2f), White);

            DrawText(">", new PointF(x - 42f, cy - 12f), 0.35f, White);
            DrawText($"{(int)speed}", new PointF(x - 30f, cy - 12f), 0.35f, White);
            DrawText("KPH", new PointF(x - 34f, cy + TAPE_ROWS * ROW_H + 5f), 0.25f, White);
        }

        private static void DrawAltitudeTape(DroneState s)
        {
            float x = ScreenW - 60f;
            float cy = ScreenH * 0.5f;
            float alt = GetAltitudeAgl(s);
            int baseVal = (int)Math.Round(alt / ALT_STEP) * (int)ALT_STEP;

            for (int i = -(int)TAPE_ROWS; i <= (int)TAPE_ROWS; i++)
            {
                int val = baseVal + i * (int)ALT_STEP;
                if (val < 0) continue;

                float frac = (alt - baseVal) / ALT_STEP;
                float y = cy - (i - frac) * ROW_H;
                if (Math.Abs(y - cy) < 12f) continue;
                if (y < cy - TAPE_ROWS * ROW_H || y > cy + TAPE_ROWS * ROW_H) continue;

                DrawOsdLine(new PointF(x, y), new SizeF(8f, 2f), White);
                DrawText(val.ToString(), new PointF(x + 12f, y - 10f), 0.30f, White);
            }

            DrawOsdLine(new PointF(x - 2f, cy - TAPE_ROWS * ROW_H), new SizeF(2f, TAPE_ROWS * ROW_H * 2f), White);

            Color altColor = alt < 5f ? Red : White;
            DrawText($"{(int)alt}", new PointF(x + 12f, cy - 12f), 0.35f, altColor);
            DrawText("<", new PointF(x + 38f, cy - 12f), 0.35f, altColor);
            DrawText("ALT", new PointF(x + 12f, cy + TAPE_ROWS * ROW_H + 5f), 0.25f, White);
        }

        private static void DrawCenterReticle()
        {
            float cx = ScreenW * 0.5f;
            float cy = ScreenH * 0.5f;

            DrawOsdLine(new PointF(cx - 28f, cy - 1f), new SizeF(12f, 2f), White);

            DrawText("O", new PointF(cx - 5.5f, cy - 10f), 0.38f, White);
        }

        private static void DrawStatusBanner(DroneState s)
        {
            string text = "ARMED"; Color c = White;

            if (s.B <= 0.001f && s.MotorsDeadBannerT > 0f) { text = "MOTORS DEAD"; c = Red; }
            else if (s.ImpactImminent) { text = "IMPACT IMMINENT"; c = Red; }
            else if (s.Stage == FlightStage.Lost) { text = "AUTONOMOUS MODE"; c = Red; }
            else if (s.Q < 0.15f) { text = "SIGNAL LOST"; c = Red; }
            else if (s.SignalRestoredBannerT > 0f) { text = "SIGNAL RESTORED"; c = White; }
            else if (s.B < 10f) { text = "CRITICAL BAT"; c = Red; }

            DrawText(text, new PointF(ScreenW * 0.5f - 50f, ScreenH - 80f), 0.38f, c);
        }

        private static void DrawBottomLeftBatteryBlock(DroneState s)
        {
            // Y = ScreenH - 240 ensures we clear the GTA V minimap completely
            float bx = 30f;
            float by = ScreenH - 240f;

            float volts = SimPackVoltage(s);
            float amps = SimAmps(s);
            Color vCol = volts < 14.0f ? Red : White;

            DrawText($"MAIN {volts:F1}V", new PointF(bx, by), 0.32f, vCol);
            DrawText($"CURR {amps:F0}A", new PointF(bx, by + 18f), 0.32f, White);
            DrawText($"CONS {s.MahUsed:F0}mAh", new PointF(bx, by + 36f), 0.32f, White);
        }

        private static void DrawBottomRightInfo(DroneState s)
        {
            float bx = ScreenW - 130f;
            float by = ScreenH - 240f;

            float vSpeed = s.V.Z;
            string vsSign = vSpeed >= 0 ? "+" : "";

            DrawText($"VAR {vsSign}{vSpeed:F1}", new PointF(bx, by), 0.32f, White);
            DrawText("VTX 12.0V", new PointF(bx, by + 18f), 0.32f, White);
            DrawText($"FLY {FormatTime(s.FlightTimerReal)}", new PointF(bx, by + 36f), 0.32f, White);
        }

        private static float SimPackVoltage(DroneState s)
        {
            float baseV = 13.2f + 3.6f * Math.Max(0f, Math.Min(100f, s.B)) / 100f;
            float sag = SimAmps(s) * 0.015f;
            return Math.Max(12.8f, baseV - sag);
        }

        private static float SimAmps(DroneState s) => (s.T / Config.TMax) * 35f;

        private static string FormatTime(float t)
        {
            int sec = (int)Math.Max(0f, t);
            return $"{sec / 60:00}:{sec % 60:00}";
        }

        private static float GetAltitudeAgl(DroneState s)
        {
            float groundZ;
            if (!World.GetGroundHeight(s.P, out groundZ, GetGroundHeightMode.Normal)) groundZ = s.P.Z;
            return Math.Max(0f, s.P.Z - groundZ);
        }

        private static float Normalize360(float deg)
        {
            while (deg < 0f) deg += 360f;
            while (deg >= 360f) deg -= 360f;
            return deg;
        }

        private static void DrawHorizon(DroneState s)
        {
            float yOff = s.Theta * Config.KHorizonScale;
            float yCenter = ScreenH / 2f + yOff;
            float angleRad = -s.PhiCam;

            const int segments = 24;
            const float halfLen = 180f;
            float dx = (float)Math.Cos(angleRad) * (halfLen * 2f / segments);
            float dy = (float)Math.Sin(angleRad) * (halfLen * 2f / segments);
            float x0 = ScreenW / 2f - (float)Math.Cos(angleRad) * halfLen;
            float y0 = yCenter - (float)Math.Sin(angleRad) * halfLen;

            for (int i = 0; i < segments; i++)
            {
                if (i % 2 == 0)
                    DrawOsdLine(new PointF(x0 + dx * i, y0 + dy * i), new SizeF(8f, 2f), White);
            }
        }

        // --- Custom OSD Rendering Helpers ---

        private static void DrawText(string text, PointF pos, float scale, Color c)
        {
            // Adding Shadow = true forces GTA UI to draw a 1px black outline/shadow
            // making it perfectly visible on bright skies without needing a background box.
            new TextElement(text, pos, scale, c, GTA.UI.Font.ChaletLondon) { Shadow = true }.Draw();
        }

        private static void DrawOsdLine(PointF pos, SizeF size, Color c)
        {
            // Draw slightly larger black background first to act as a hard outline
            new ContainerElement(new PointF(pos.X - 1f, pos.Y - 1f), new SizeF(size.Width + 2f, size.Height + 2f), Color.Black).Draw();
            // Draw actual white line
            new ContainerElement(pos, size, c).Draw();
        }
    }
}
