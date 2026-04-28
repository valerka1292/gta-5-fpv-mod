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

        private static readonly Color White  = Color.FromArgb(220, 230, 230, 230);
        private static readonly Color Green  = Color.FromArgb(220, 80, 220, 80);
        private static readonly Color Yellow = Color.FromArgb(220, 240, 200, 60);
        private static readonly Color Orange = Color.FromArgb(220, 240, 140, 40);
        private static readonly Color Red    = Color.FromArgb(220, 230, 60, 60);
        private static readonly Color Dim    = Color.FromArgb(120, 170, 170, 170);

        public static void Draw(DroneState s, float fpvFov)
        {
            DrawTopRow(s);
            DrawCompassTape(s);
            DrawSpeedTape(s);
            DrawAltitudeTape(s);
            DrawCenterReticle();
            DrawStatusBanner(s);
            DrawHorizon(s);
            DrawBottomLeftBatteryBlock(s);
            DrawBottomRightInfo(s);
        }

        private static void DrawTopRow(DroneState s)
        {
            float topY = 20f;
            DrawGpsCoords(s, new PointF(ScreenW * 0.03f, topY));

            float distHome = (s.P - s.Spawn).Length();
            Color homeColor = distHome > (s.LoS ? Config.DCritField : Config.DCritCity) ? Yellow : White;
            DrawText($"HOME {distHome,5:F0}m", new PointF(ScreenW * 0.39f, topY), 0.32f, homeColor);

            int rssi = (int)Math.Round(Math.Max(0f, Math.Min(1f, s.Q)) * 100f);
            Color rssiColor = rssi < 25 ? Red : (rssi < 45 ? Yellow : White);
            DrawText($"RSSI {rssi,3}%", new PointF(ScreenW * 0.86f, topY), 0.32f, rssiColor);
        }

        private static void DrawGpsCoords(DroneState s, PointF pos)
        {
            DrawText($"LAT {s.P.Y,8:F5}", pos, 0.30f, White);
            DrawText($"LON {s.P.X,8:F5}", new PointF(pos.X, pos.Y + 15f), 0.30f, White);
        }

        private static void DrawCompassTape(DroneState s)
        {
            float cx = ScreenW * 0.5f;
            float y = 56f;
            float headingDeg = Normalize360(s.Psi * MathF.Rad2Deg);

            const float pxPerDeg = 3.2f;
            const float halfW = 160f;
            new ContainerElement(new PointF(cx - halfW, y), new SizeF(halfW * 2f, 1f), Dim).Draw();

            for (int d = -50; d <= 50; d += 5)
            {
                float x = cx + d * pxPerDeg;
                bool major = d % 10 == 0;
                float h = major ? 10f : 5f;
                new ContainerElement(new PointF(x, y - h), new SizeF(1f, h), White).Draw();

                if (major)
                {
                    int mark = (int)Normalize360(headingDeg + d);
                    DrawText(mark.ToString("000"), new PointF(x - 10f, y - 23f), 0.22f, Dim);
                }
            }

            DrawText($"▼{(int)headingDeg:000}°", new PointF(cx - 26f, y + 4f), 0.33f, White);
        }

        private static void DrawSpeedTape(DroneState s)
        {
            float x = 44f;
            float cy = ScreenH * 0.5f;
            float speed = s.V.Length() * 3.6f;

            new ContainerElement(new PointF(x, cy - 95f), new SizeF(40f, 190f), Color.FromArgb(30, 255, 255, 255)).Draw();

            for (int k = -4; k <= 4; k++)
            {
                float yy = cy + k * 22f;
                new ContainerElement(new PointF(x + 4f, yy), new SizeF(8f, 1f), Dim).Draw();
                float val = Math.Max(0f, speed - k * 10f);
                DrawText(((int)val).ToString(), new PointF(x + 16f, yy - 8f), 0.23f, Dim);
            }

            DrawBox(new PointF(x + 2f, cy - 12f), new SizeF(58f, 24f), Color.FromArgb(35, 0, 0, 0));
            DrawText($"{speed,4:F0}", new PointF(x + 10f, cy - 8f), 0.34f, White);
        }

        private static void DrawAltitudeTape(DroneState s)
        {
            float x = ScreenW - 84f;
            float cy = ScreenH * 0.5f;
            float alt = GetAltitudeAgl(s);

            new ContainerElement(new PointF(x, cy - 95f), new SizeF(40f, 190f), Color.FromArgb(30, 255, 255, 255)).Draw();

            for (int k = -4; k <= 4; k++)
            {
                float yy = cy + k * 22f;
                new ContainerElement(new PointF(x + 28f, yy), new SizeF(8f, 1f), Dim).Draw();
                float val = Math.Max(0f, alt - k * 5f);
                DrawText(((int)val).ToString(), new PointF(x - 20f, yy - 8f), 0.23f, Dim);
            }

            DrawBox(new PointF(x - 54f, cy - 12f), new SizeF(58f, 24f), Color.FromArgb(35, 0, 0, 0));
            Color c = alt < 3f ? Orange : White;
            DrawText($"{alt,4:F0}", new PointF(x - 46f, cy - 8f), 0.34f, c);
        }

        private static void DrawCenterReticle()
        {
            float cx = ScreenW * 0.5f;
            float cy = ScreenH * 0.5f;
            new ContainerElement(new PointF(cx - 16f, cy), new SizeF(12f, 1.5f), White).Draw();
            new ContainerElement(new PointF(cx + 4f, cy), new SizeF(12f, 1.5f), White).Draw();
            new ContainerElement(new PointF(cx - 1f, cy - 1f), new SizeF(2f, 2f), White).Draw();
            DrawText("○", new PointF(cx - 5f, cy - 8f), 0.28f, White);
        }

        private static void DrawStatusBanner(DroneState s)
        {
            string text = "[  ARMED  ]";
            Color c = Green;

            if (s.B <= 0.001f && s.MotorsDeadBannerT > 0f) { text = "[ MOTORS DEAD ]"; c = Red; }
            else if (s.ImpactImminent) { text = "[ IMPACT IMMINENT ]"; c = Red; }
            else if (s.Stage == FlightStage.Lost) { text = "[ AUTONOMOUS MODE ]"; c = Red; }
            else if (s.Q < 0.15f) { text = "[ SIGNAL LOST ]"; c = Red; }
            else if (s.SignalRestoredBannerT > 0f) { text = "[ SIGNAL RESTORED ]"; c = Green; }
            else if (s.B < 10f) { text = "[ CRITICAL BAT ]"; c = Red; }
            else if (s.B < 25f) { text = "[ LOW BAT ]"; c = Yellow; }

            DrawText(text, new PointF(ScreenW * 0.5f - 72f, ScreenH * 0.5f - 42f), 0.36f, c);
        }

        private static void DrawBottomLeftBatteryBlock(DroneState s)
        {
            // Поднято выше, чтобы не пересекаться с миникартой GTA.
            float bx = 28f;
            float by = ScreenH - 156f;
            DrawBatteryBlock(s, bx, by);
        }

        private static void DrawBatteryBlock(DroneState s, float bx, float by)
        {
            DrawBox(new PointF(bx - 4f, by - 2f), new SizeF(100f, 52f), Color.FromArgb(45, 0, 0, 0));

            float volts = SimPackVoltage(s);
            float amps = SimAmps(s);
            Color vCol = volts < 14.2f ? Yellow : White;
            Color aCol = amps > 28f ? Yellow : White;

            DrawText($"[P] {volts:F1}V", new PointF(bx, by), 0.30f, vCol);
            DrawText($"    {amps:F1}A", new PointF(bx, by + 14f), 0.30f, aCol);
            DrawText($" {s.MahUsed:F0} mAh", new PointF(bx, by + 28f), 0.30f, White);
        }

        private static void DrawBottomRightInfo(DroneState s)
        {
            float bx = ScreenW - 170f;
            float by = ScreenH - 156f;
            DrawBox(new PointF(bx - 4f, by - 2f), new SizeF(145f, 52f), Color.FromArgb(45, 0, 0, 0));

            float vSpeed = s.V.Z;
            Color vsCol = Math.Abs(vSpeed) > 5f ? Yellow : White;
            DrawText($"VAR {vSpeed,5:F1}m/s", new PointF(bx, by), 0.30f, vsCol);
            DrawText($"VTX {12.0f:F1}V", new PointF(bx, by + 14f), 0.30f, White);
            DrawText($"TMR {FormatTime(s.FlightTimerReal)}", new PointF(bx, by + 28f), 0.30f, White);
        }

        private static float SimPackVoltage(DroneState s)
        {
            // 4S LiPo: от ~16.8 В (100%) до ~13.2 В (0%), с небольшой просадкой под током.
            float baseV = 13.2f + 3.6f * Math.Max(0f, Math.Min(100f, s.B)) / 100f;
            float sag = SimAmps(s) * 0.01f;
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

            const int segments = 28;
            const float halfLen = 160f;
            float dx = (float)Math.Cos(angleRad) * (halfLen * 2f / segments);
            float dy = (float)Math.Sin(angleRad) * (halfLen * 2f / segments);
            float x0 = ScreenW / 2f - (float)Math.Cos(angleRad) * halfLen;
            float y0 = yCenter - (float)Math.Sin(angleRad) * halfLen;

            for (int i = 0; i < segments; i++)
                new ContainerElement(new PointF(x0 + dx * i, y0 + dy * i), new SizeF(5f, 1f), White).Draw();
        }

        private static void DrawText(string text, PointF pos, float scale, Color c)
        {
            new TextElement(text, pos, scale, c, GTA.UI.Font.ChaletLondon).Draw();
        }

        private static void DrawBox(PointF pos, SizeF size, Color color)
        {
            new ContainerElement(pos, size, color).Draw();
        }
    }
}
