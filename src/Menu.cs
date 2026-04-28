using System;
using System.Collections.Generic;
using System.Drawing;
using GTA.UI;
using Font = GTA.UI.Font;

namespace FpvDroneMod
{
    // Lightweight in-house settings menu. Avoids pulling in NativeUI.dll —
    // that library targets SHVDN v2 in this user's setup, and bridging across
    // API versions inside one assembly is more pain than the menu is worth.
    //
    // Controls (handled in Main.OnKeyDown):
    //   F9          — toggle (only when not flying)
    //   ↑/↓         — move selection
    //   ←/→         — change current item value
    //   Esc / F9    — close
    internal static class Menu
    {
        private static bool _open;
        private static int _selected;

        public static bool IsOpen => _open;

        // Each entry knows how to advertise + cycle its own value.
        private interface IItem
        {
            string Title { get; }
            string Value { get; }
            void OnLeft();
            void OnRight();
        }

        private sealed class ListItem : IItem
        {
            public string Title { get; }
            private readonly Func<int> _getIdx;
            private readonly Action<int> _setIdx;
            private readonly int _count;
            private readonly Func<int, string> _format;

            public ListItem(string title, Func<int> getIdx, Action<int> setIdx,
                            int count, Func<int, string> format)
            {
                Title = title; _getIdx = getIdx; _setIdx = setIdx;
                _count = count; _format = format;
            }

            public string Value => _format(_getIdx());

            public void OnLeft()  { _setIdx((_getIdx() - 1 + _count) % _count); }
            public void OnRight() { _setIdx((_getIdx() + 1) % _count); }
        }

        private static readonly List<IItem> Items = new List<IItem>
        {
            new ListItem(
                "Explosion type",
                () => Settings.ExplosionPresetIndex,
                (i) => Settings.ExplosionPresetIndex = i,
                Settings.ExplosionPresets.Length,
                (i) => $"{Settings.ExplosionPresets[i].Name} (id {Settings.ExplosionPresets[i].Id})"),
        };

        public static void Toggle()
        {
            _open = !_open;
            if (_open) _selected = 0;
        }

        public static void Close() { _open = false; }

        // Returns true if this menu consumed the key (caller should not run
        // its own handlers).
        public static bool OnKey(System.Windows.Forms.Keys key)
        {
            if (!_open) return false;
            switch (key)
            {
                case System.Windows.Forms.Keys.Up:
                    _selected = (_selected - 1 + Items.Count) % Items.Count;
                    return true;
                case System.Windows.Forms.Keys.Down:
                    _selected = (_selected + 1) % Items.Count;
                    return true;
                case System.Windows.Forms.Keys.Left:
                    Items[_selected].OnLeft();
                    return true;
                case System.Windows.Forms.Keys.Right:
                    Items[_selected].OnRight();
                    return true;
                case System.Windows.Forms.Keys.Escape:
                case System.Windows.Forms.Keys.F9:
                    Close();
                    return true;
            }
            return false;
        }

        public static void Draw()
        {
            if (!_open) return;

            // Use the live UI canvas (Screen.Width × Screen.Height = 720-base).
            float W = Screen.Width;
            float H = Screen.Height;

            const float menuW   = 380f;
            const float headerH = 28f;
            const float rowH    = 22f;
            const float padBot  = 10f;
            float menuH = headerH + Items.Count * rowH + padBot;
            float x = (W - menuW) / 2f;
            float y = (H - menuH) / 2f;

            // Header strip (green) + body (translucent black).
            new ContainerElement(new PointF(x, y), new SizeF(menuW, headerH),
                Color.FromArgb(220, 60, 100, 60)).Draw();
            new ContainerElement(new PointF(x, y + headerH),
                new SizeF(menuW, menuH - headerH),
                Color.FromArgb(200, 0, 0, 0)).Draw();

            new TextElement("FPV Drone — Settings",
                new PointF(x + 10, y + 5), 0.42f,
                Color.White, Font.ChaletLondon).Draw();

            for (int i = 0; i < Items.Count; i++)
            {
                float ry = y + headerH + 4f + i * rowH;
                Color textColor;

                if (i == _selected)
                {
                    new ContainerElement(
                        new PointF(x, ry - 2f),
                        new SizeF(menuW, rowH),
                        Color.FromArgb(180, 100, 180, 100)).Draw();
                    textColor = Color.Black;
                }
                else
                {
                    textColor = Color.White;
                }

                new TextElement(Items[i].Title,
                    new PointF(x + 10f, ry), 0.38f,
                    textColor, Font.ChaletLondon).Draw();

                new TextElement($"<  {Items[i].Value}  >",
                    new PointF(x + 150f, ry), 0.38f,
                    textColor, Font.ChaletLondon).Draw();
            }

            new TextElement(
                "[Up/Down] select   [Left/Right] change   [F9 / Esc] close",
                new PointF(x + 10f, y + menuH + 4f),
                0.30f, Color.LightGray, Font.ChaletLondon).Draw();
        }
    }
}
