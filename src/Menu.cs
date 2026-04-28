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
                _currentCategoryIndex = 0;
                _currentPayloadIndex = 0;
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
                        _currentCategoryIndex = (_currentCategoryIndex - 1 + Settings.Categories.Length)
                                                % Settings.Categories.Length;
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
                        _currentCategoryIndex = (_currentCategoryIndex + 1) % Settings.Categories.Length;
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
                        _state = MenuState.PayloadView;
                        _currentPayloadIndex = 0;
                    }
                    else
                    {
                        var selected = Settings.Categories[_currentCategoryIndex].Presets[_currentPayloadIndex];
                        Settings.ActivePayload = selected;
                        _state = MenuState.CategoryView;
                    }
                    return true;

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
                ? Settings.Categories.Length
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
                for (int i = 0; i < Settings.Categories.Length; i++)
                {
                    float ry = y + headerH + 4f + i * rowH;
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
