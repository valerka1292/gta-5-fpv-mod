using System;
using System.Drawing;
using System.Windows.Forms;
using GTA.UI;
using Font = GTA.UI.Font;

namespace FpvDroneMod
{
    internal static class Menu
    {
        private enum MenuState
        {
            CategoryView,
            PayloadView
        }

        private static bool _open;
        private static MenuState _state = MenuState.CategoryView;
        private static int _currentCategoryIndex;
        private static int _currentPayloadIndex;

        public static bool IsOpen => _open;

        public static void Toggle()
        {
            _open = !_open;
            if (_open)
            {
                _state = MenuState.CategoryView;
                _currentCategoryIndex = -3;
                _currentPayloadIndex = 0;

                // Если телефон открыт - закрываем его нафиг
                Natives.CloseCellPhone();
            }
        }

        public static void Close() { _open = false; }

        // Returns true if this menu consumed the key (caller should not run
        // its own handlers).
        public static bool OnKey(Keys key)
        {
            if (!_open) return false;

            switch (key)
            {
                case Keys.Up:
                    if (_state == MenuState.CategoryView)
                    {
                        _currentCategoryIndex--;
                        if (_currentCategoryIndex < -4)
                            _currentCategoryIndex = Settings.Categories.Length - 1;
                    }
                    else
                    {
                        var presets = Settings.Categories[_currentCategoryIndex].Presets;
                        _currentPayloadIndex = (_currentPayloadIndex - 1 + presets.Length) % presets.Length;
                    }
                    return true;

                case Keys.Down:
                    if (_state == MenuState.CategoryView)
                    {
                        _currentCategoryIndex++;
                        if (_currentCategoryIndex >= Settings.Categories.Length)
                            _currentCategoryIndex = -4;
                    }
                    else
                    {
                        var presets = Settings.Categories[_currentCategoryIndex].Presets;
                        _currentPayloadIndex = (_currentPayloadIndex + 1) % presets.Length;
                    }
                    return true;

                case Keys.Enter:
                    if (_state == MenuState.CategoryView)
                    {
                        if (_currentCategoryIndex == -4)
                        {
                            Settings.ActiveProfileIndex = (Settings.ActiveProfileIndex + 1) % Settings.DroneProfiles.Length;
                        }
                        else if (_currentCategoryIndex == -3)
                        {
                            int impulseIdx = Settings.ImpulseScaleIndex;
                            if (impulseIdx < 0) impulseIdx = 0;
                            if (impulseIdx >= Settings.ImpulsePresets.Length) impulseIdx = Settings.ImpulsePresets.Length - 1;
                            Settings.ImpulseScaleIndex = (impulseIdx + 1) % Settings.ImpulsePresets.Length;
                        }
                        else if (_currentCategoryIndex == -2)
                        {
                            Settings.GiveStars = !Settings.GiveStars;
                        }
                        else if (_currentCategoryIndex == -1)
                        {
                            Settings.DamageOverrideIndex = (Settings.DamageOverrideIndex + 1) % Settings.DamageValues.Length;
                        }
                        else
                        {
                            _state = MenuState.PayloadView;
                            _currentPayloadIndex = 0;
                        }
                    }
                    else
                    {
                        var selected = Settings.Categories[_currentCategoryIndex].Presets[_currentPayloadIndex];
                        Settings.ActivePayload = selected;
                        _state = MenuState.CategoryView;
                    }
                    return true;

                case Keys.Left:
                    if (_state == MenuState.CategoryView && _currentCategoryIndex == -4)
                    {
                        Settings.ActiveProfileIndex--;
                        if (Settings.ActiveProfileIndex < 0)
                            Settings.ActiveProfileIndex = Settings.DroneProfiles.Length - 1;
                        return true;
                    }
                    if (_state == MenuState.CategoryView && _currentCategoryIndex == -3)
                    {
                        int impulseIdx = Settings.ImpulseScaleIndex;
                        if (impulseIdx < 0) impulseIdx = 0;
                        if (impulseIdx >= Settings.ImpulsePresets.Length) impulseIdx = Settings.ImpulsePresets.Length - 1;
                        impulseIdx--;
                        if (impulseIdx < 0) impulseIdx = Settings.ImpulsePresets.Length - 1;
                        Settings.ImpulseScaleIndex = impulseIdx;
                        return true;
                    }
                    if (_state == MenuState.CategoryView && _currentCategoryIndex == -1)
                    {
                        Settings.DamageOverrideIndex--;
                        if (Settings.DamageOverrideIndex < 0)
                            Settings.DamageOverrideIndex = Settings.DamageValues.Length - 1;
                        return true;
                    }
                    return false;

                case Keys.Right:
                    if (_state == MenuState.CategoryView && _currentCategoryIndex == -4)
                    {
                        Settings.ActiveProfileIndex = (Settings.ActiveProfileIndex + 1) % Settings.DroneProfiles.Length;
                        return true;
                    }
                    if (_state == MenuState.CategoryView && _currentCategoryIndex == -3)
                    {
                        int impulseIdx = Settings.ImpulseScaleIndex;
                        if (impulseIdx < 0) impulseIdx = 0;
                        if (impulseIdx >= Settings.ImpulsePresets.Length) impulseIdx = Settings.ImpulsePresets.Length - 1;
                        Settings.ImpulseScaleIndex = (impulseIdx + 1) % Settings.ImpulsePresets.Length;
                        return true;
                    }
                    if (_state == MenuState.CategoryView && _currentCategoryIndex == -1)
                    {
                        Settings.DamageOverrideIndex = (Settings.DamageOverrideIndex + 1) % Settings.DamageValues.Length;
                        return true;
                    }
                    return false;

                case Keys.Back:
                    if (_state == MenuState.PayloadView)
                        _state = MenuState.CategoryView;
                    else
                        Close();
                    return true;

                case Keys.Escape:
                case Keys.F9:
                    Close();
                    return true;
            }

            return false;
        }

        public static void Draw()
        {
            if (!_open) return;

            float W = GTA.UI.Screen.Width;
            float H = GTA.UI.Screen.Height;

            const float menuW = 500f;
            const float headerH = 28f;
            const float rowH = 22f;
            const float padBot = 10f;

            int rows = _state == MenuState.CategoryView
                ? Settings.Categories.Length + 4
                : Settings.Categories[_currentCategoryIndex].Presets.Length;

            float menuH = headerH + rows * rowH + padBot;
            float x = (W - menuW) / 2f;
            float y = (H - menuH) / 2f;

            new ContainerElement(new PointF(x, y), new SizeF(menuW, headerH),
                Color.FromArgb(220, 60, 100, 60)).Draw();
            new ContainerElement(new PointF(x, y + headerH),
                new SizeF(menuW, menuH - headerH),
                Color.FromArgb(200, 0, 0, 0)).Draw();

            new TextElement("FPV Drone — Полезная нагрузка дрона",
                new PointF(x + 10, y + 5), 0.42f,
                Color.White, Font.ChaletLondon).Draw();

            if (_state == MenuState.CategoryView)
            {
                float forceY = y + headerH + 4f;
                bool forceSelected = _currentCategoryIndex == -3;
                Color forceColor = forceSelected ? Color.Black : Color.White;

                if (forceSelected)
                {
                    new ContainerElement(
                        new PointF(x, forceY - 2f),
                        new SizeF(menuW, rowH),
                        Color.FromArgb(180, 100, 180, 100)).Draw();
                }

                int impulseIdx = Settings.ImpulseScaleIndex;
                if (impulseIdx < 0) impulseIdx = 0;
                if (impulseIdx >= Settings.ImpulsePresets.Length) impulseIdx = Settings.ImpulsePresets.Length - 1;

                new TextElement("FORCE (IMPULSE)",
                    new PointF(x + 10f, forceY), 0.38f,
                    forceColor, Font.ChaletLondon).Draw();

                new TextElement($"<  {Settings.ImpulsePresets[impulseIdx].Label}  >",
                    new PointF(x + 290f, forceY), 0.38f,
                    forceColor, Font.ChaletLondon).Draw();

                float starsRowY = forceY + rowH;
                bool starsSelected = _currentCategoryIndex == -2;
                Color starsColor = starsSelected ? Color.Black : Color.White;

                if (starsSelected)
                {
                    new ContainerElement(
                        new PointF(x, starsRowY - 2f),
                        new SizeF(menuW, rowH),
                        Color.FromArgb(180, 100, 180, 100)).Draw();
                }

                new TextElement("WANTED STARS",
                    new PointF(x + 10f, starsRowY), 0.38f,
                    starsColor, Font.ChaletLondon).Draw();

                string starsValue = Settings.GiveStars ? "ENABLED" : "DISABLED";
                new TextElement($"<  {starsValue}  >",
                    new PointF(x + 290f, starsRowY), 0.38f,
                    starsColor, Font.ChaletLondon).Draw();

                float damageY = starsRowY + rowH;
                bool damageSelected = _currentCategoryIndex == -1;
                Color damageColor = damageSelected ? Color.Black : Color.White;

                if (damageSelected)
                {
                    new ContainerElement(
                        new PointF(x, damageY - 2f),
                        new SizeF(menuW, rowH),
                        Color.FromArgb(180, 100, 180, 100)).Draw();
                }

                int dmgIdx = Settings.DamageOverrideIndex;
                if (dmgIdx < 0) dmgIdx = 0;
                if (dmgIdx >= Settings.DamageValues.Length) dmgIdx = Settings.DamageValues.Length - 1;
                float hpValue = Settings.DamageValues[dmgIdx];

                new TextElement("DAMAGE (HP)",
                    new PointF(x + 10f, damageY), 0.38f,
                    damageColor, Font.ChaletLondon).Draw();
                new TextElement($"<  {hpValue:0}  >",
                    new PointF(x + 290f, damageY), 0.38f,
                    damageColor, Font.ChaletLondon).Draw();

                // ── DRONE CLASS ──────────────────────────────────────────────
                float classY = damageY + rowH;
                bool classSelected = _currentCategoryIndex == -4;
                Color classColor = classSelected ? Color.Black : Color.White;

                if (classSelected)
                {
                    new ContainerElement(
                        new PointF(x, classY - 2f),
                        new SizeF(menuW, rowH),
                        Color.FromArgb(180, 100, 180, 100)).Draw();
                }

                var prof = Settings.CurrentProfile;
                int speedKph = (int)Math.Round(prof.TMax * 3.6f);

                new TextElement("DRONE CLASS",
                    new PointF(x + 10f, classY), 0.38f,
                    classColor, Font.ChaletLondon).Draw();
                new TextElement($"<  {prof.Label}  ({speedKph} KPH)  >",
                    new PointF(x + 180f, classY), 0.38f,
                    classColor, Font.ChaletLondon).Draw();

                for (int i = 0; i < Settings.Categories.Length; i++)
                {
                    float ry = y + headerH + 4f + (i + 4) * rowH;
                    Color textColor = i == _currentCategoryIndex ? Color.Black : Color.White;

                    if (i == _currentCategoryIndex)
                    {
                        new ContainerElement(
                            new PointF(x, ry - 2f),
                            new SizeF(menuW, rowH),
                            Color.FromArgb(180, 100, 180, 100)).Draw();
                    }

                    new TextElement(Settings.Categories[i].Name,
                        new PointF(x + 10f, ry), 0.38f,
                        textColor, Font.ChaletLondon).Draw();
                }
            }
            else
            {
                var category = Settings.Categories[_currentCategoryIndex];
                for (int i = 0; i < category.Presets.Length; i++)
                {
                    float ry = y + headerH + 4f + i * rowH;
                    bool isSelected = i == _currentPayloadIndex;
                    var preset = category.Presets[i];
                    bool isActive = preset.Id == Settings.CurrentExplosionId
                                    && preset.Name == Settings.CurrentExplosionName;

                    Color textColor = isSelected ? Color.Black : Color.White;
                    if (isSelected)
                    {
                        new ContainerElement(
                            new PointF(x, ry - 2f),
                            new SizeF(menuW, rowH),
                            Color.FromArgb(180, 100, 180, 100)).Draw();
                    }

                    string suffix = isActive ? " [Выбрано]" : string.Empty;
                    new TextElement($"{preset.Name} (id {preset.Id}){suffix}",
                        new PointF(x + 10f, ry), 0.36f,
                        textColor, Font.ChaletLondon).Draw();
                }
            }

            new TextElement(
                "[Вверх/Вниз] Выбор   [Enter] Подтвердить   [Backspace] Назад   [F9/Esc] Закрыть",
                new PointF(x + 10f, y + menuH + 4f),
                0.30f, Color.LightGray, Font.ChaletLondon).Draw();
        }
    }
}
