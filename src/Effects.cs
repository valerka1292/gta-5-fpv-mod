using System;
using System.Drawing;
using GTA;
using GTA.UI;

namespace FpvDroneMod
{
    // Spec section 11. Interference effects — driven by I.
    //
    // Honest CRT post-process is impossible without an external shader hook,
    // so we approximate (M2):
    //  - Discrete ANIMPOSTFX presets at 3 escalating tiers.
    //  - UIRectangle overlays for noise / scanlines / glitch / blackout.
    internal static class Effects
    {
        private static readonly Random Rng = new Random();

        // Tier 0: no effect (I < 0.50). We make sure to STOP previous effects.
        // Tier 1: 0.50 ≤ I < 0.70 — chromatic-ish wash.
        // Tier 2: 0.70 ≤ I < 0.85 — heavier wash + tint.
        // Tier 3: I ≥ 0.85         — death-fail border, cranks the dial.
        private static int _currentTier = -1;
        private static readonly string[] TierFx = { null, "DrugsMichaelAliensFightIn", "MP_Bull_tox", "DeathFailOut" };

        public static void Update(float I)
        {
            int tier = 0;
            if (I >= 0.85f) tier = 3;
            else if (I >= 0.70f) tier = 2;
            else if (I >= 0.50f) tier = 1;

            if (tier == _currentTier) return;

            // Stop previous (skip null at tier 0).
            if (_currentTier > 0 && _currentTier < TierFx.Length)
                Natives.AnimpostfxStop(TierFx[_currentTier]);

            if (tier > 0)
                Natives.AnimpostfxPlay(TierFx[tier], 0, true);

            _currentTier = tier;
        }

        public static void StopAll()
        {
            Natives.AnimpostfxStopAll();
            _currentTier = -1;
            _vSyncRollY = 0f;
            _blackoutUntil = 0f;
        }

        // Per-frame UI overlays. Use the live UI canvas dimensions
        private static float W => GTA.UI.Screen.Width;
        private static float H => GTA.UI.Screen.Height;

        private static float _vSyncRollY = 0f;
        private static float _blackoutUntil = 0f;

        public static void DrawOverlays(float I, int walls)
        {
            if (I < 0.02f && walls == 0) return;

            DrawAnalogStatic(I, walls);

            // Multipathing from signal reflections off walls creates heavy frame tearing.
            if (walls > 0 || I > 0.3f)
            {
                DrawMultipathTearing(I, walls);
                if (I > 0.4f || walls > 1) DrawChromaShift(I, walls);
            }

            // Vertical sync loss: a rolling frame band.
            if (I > 0.5f)
            {
                DrawVSyncRoll(I);
            }

            if (I > 0.75f)
            {
                MaybeDrawBlackout(I);
            }
        }

        // 11.1 Analog white noise: horizontal strokes instead of square pixels.
        private static void DrawAnalogStatic(float I, int walls)
        {
            // Fewer elements, longer lines: cheaper for CPU and closer to analog static.
            int count = (int)(I * 120) + (walls * 15);
            if (count > 200) count = 200;

            int baseAlpha = (int)(30 + I * 100);

            for (int i = 0; i < count; i++)
            {
                float x = (float)(Rng.NextDouble() * W);
                float y = (float)(Rng.NextDouble() * H);
                float length = 10f + (float)(Rng.NextDouble() * 150f);
                float height = 1f + (float)(Rng.NextDouble() * 2f);

                int alpha = Math.Min(255, baseAlpha + Rng.Next(0, 50));
                Color c = Color.FromArgb(alpha, 200, 200, 200);

                new ContainerElement(new PointF(x, y), new SizeF(length, height), c).Draw();
            }
        }

        // 11.2 Multipathing: flickering horizontal tearing from obstacles.
        private static void DrawMultipathTearing(float I, int walls)
        {
            int bands = (int)(walls * 1.5f) + (int)(I * 4f);
            if (bands > 10) bands = 10;

            for (int i = 0; i < bands; i++)
            {
                // Bands flicker heavily.
                if (Rng.NextDouble() > 0.4) continue;

                float y = (float)Rng.NextDouble() * H;
                float h = 4f + (float)Rng.NextDouble() * 30f;

                int alpha = (int)(30 + I * 60 + walls * 20);
                alpha = Math.Min(220, alpha);
                Color c = Color.FromArgb(alpha, 180, 180, 190);

                new ContainerElement(new PointF(0, y), new SizeF(W, h), c).Draw();
            }
        }

        // 11.3 Chroma loss: magenta and green artifact bands.
        private static void DrawChromaShift(float I, int walls)
        {
            float chance = (I * 1.5f) + (walls * 0.1f);
            if (Rng.NextDouble() > chance) return;

            float y = (float)Rng.NextDouble() * H;
            float h = 10f + (float)Rng.NextDouble() * 50f;

            int alpha = (int)(20 + I * 40);
            alpha = Math.Min(100, alpha);

            // Alternate magenta and green chroma artifacts.
            Color c = Rng.NextDouble() > 0.5
                ? Color.FromArgb(alpha, 255, 0, 255)
                : Color.FromArgb(alpha, 0, 255, 0);

            new ContainerElement(new PointF(0, y), new SizeF(W, h), c).Draw();
        }

        // 11.4 V-Sync loss: wide rolling dark band.
        private static void DrawVSyncRoll(float I)
        {
            // Roll speed depends on interference intensity.
            float rollSpeed = 150f + (I * 300f);
            _vSyncRollY = (_vSyncRollY + Game.LastFrameTime * rollSpeed) % H;

            int alpha = (int)(I * 80);
            alpha = Math.Min(150, alpha);
            Color c = Color.FromArgb(alpha, 15, 15, 15);

            // Main dark sync-loss band.
            new ContainerElement(new PointF(0, _vSyncRollY), new SizeF(W, 60f), c).Draw();
            // Bright white spark at the tear boundary.
            new ContainerElement(new PointF(0, _vSyncRollY + 58f), new SizeF(W, 2f), Color.FromArgb(alpha, 255, 255, 255)).Draw();
        }

        // 11.5 Full blackout for a fraction of a second.
        private static void MaybeDrawBlackout(float I)
        {
            float now = Game.GameTime / 1000.0f;
            if (now < _blackoutUntil)
            {
                new ContainerElement(new PointF(0, 0), new SizeF(W, H),
                    Color.FromArgb(255, 10, 10, 10)).Draw();
                return;
            }

            float perFrame = ((I - 0.75f) / 0.25f) * 3f * Game.LastFrameTime;
            if (Rng.NextDouble() < perFrame)
            {
                // Short black-screen flashes (40 to 150 ms).
                float duration = 0.04f + (float)Rng.NextDouble() * 0.11f;
                _blackoutUntil = now + duration;
            }
        }
    }
}
