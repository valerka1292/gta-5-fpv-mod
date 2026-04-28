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
        }

        // Per-frame UI overlays. Use the live UI canvas dimensions
        // (Screen.Width × Screen.Height) so overlays cover the whole screen
        // independent of game resolution / aspect ratio.
        private static float W => GTA.UI.Screen.Width;
        private static float H => GTA.UI.Screen.Height;

        public static void DrawOverlays(float I)
        {
            if (I < 0.05f) return;

            DrawNoise(I);
            if (I > 0.25f) DrawScanlines(I);
            if (I > 0.60f) MaybeDrawGlitchBand(I);
            if (I > 0.75f) MaybeDrawBlackout(I);
        }

        // 11.1 Noise: scattered grey rectangles of sub-pixel density.
        private static void DrawNoise(float I)
        {
            float density = I * 0.30f;
            int count = (int)(density * 220);  // scaled to 1080p
            int alpha = (int)(40 + I * 110);
            Color c = Color.FromArgb(alpha, 200, 200, 200);

            for (int i = 0; i < count; i++)
            {
                float x = (float)(Rng.NextDouble() * W);
                float y = (float)(Rng.NextDouble() * H);
                new ContainerElement(new PointF(x, y), new SizeF(2, 2), c).Draw();
            }
        }

        // 11.2 Scanlines: faint horizontal stripes drifting downward.
        private static float _scanlineY;
        private static void DrawScanlines(float I)
        {
            int stripeCount = (int)Math.Floor(((I - 0.25f) / 0.75f) * 5f);
            if (stripeCount <= 0) return;

            _scanlineY = (_scanlineY + Game.LastFrameTime * 60f) % H;
            int alpha = (int)((I - 0.25f) * 120);
            Color c = Color.FromArgb(alpha, 180, 200, 180);

            float spacing = H / stripeCount;
            for (int i = 0; i < stripeCount; i++)
            {
                float y = (i * spacing + _scanlineY) % H;
                new ContainerElement(new PointF(0, y), new SizeF(W, 3), c).Draw();
            }
        }

        // 11.3 Glitch band — random horizontal slice on probability per frame.
        private static void MaybeDrawGlitchBand(float I)
        {
            float perFrame = ((I - 0.60f) / 0.40f) * 2f * Game.LastFrameTime;
            if (Rng.NextDouble() > perFrame) return;

            float y = (float)Rng.NextDouble() * H;
            float bandH = 10f + (float)Rng.NextDouble() * 50f;
            int alpha = (int)(80 + I * 80);
            Color c = Color.FromArgb(alpha, 60, 240, 60);
            new ContainerElement(new PointF(0, y), new SizeF(W, bandH), c).Draw();
        }

        // 11.4 Blackout — short full-screen black rectangle.
        private static float _blackoutUntil;
        private static void MaybeDrawBlackout(float I)
        {
            float now = Game.GameTime / 1000.0f;
            if (now < _blackoutUntil)
            {
                new ContainerElement(new PointF(0, 0), new SizeF(W, H),
                    Color.FromArgb(255, 0, 0, 0)).Draw();
                return;
            }

            float perFrame = ((I - 0.75f) / 0.25f) * 3f * Game.LastFrameTime;
            if (Rng.NextDouble() < perFrame)
            {
                float duration = 0.04f + (float)Rng.NextDouble() * 0.11f;
                _blackoutUntil = now + duration;
            }
        }
    }
}
