using System.Collections.Generic;

namespace FpvDroneMod
{
    internal static class Settings
    {
        public struct ExplosionPreset
        {
            public int Id;
            public string Name;
            public ExplosionPreset(int id, string name) { Id = id; Name = name; }
        }

        public class PayloadCategory
        {
            public string Name;
            public ExplosionPreset[] Presets;
            public PayloadCategory(string name, ExplosionPreset[] presets)
            {
                Name = name;
                Presets = presets;
            }
        }

        // State to keep track of selected payload
        public static ExplosionPreset ActivePayload = new ExplosionPreset(1, "💣 Граната");

        public static int CurrentExplosionId => ActivePayload.Id;
        public static string CurrentExplosionName => ActivePayload.Name;

        public static readonly PayloadCategory[] Categories = new PayloadCategory[]
        {
            new PayloadCategory("🔴 Супер-оружие", new ExplosionPreset[] {
                new ExplosionPreset(78, "☢️ Ядерный взрыв"),
                new ExplosionPreset(60, "🛰️ Орбитальная пушка"),
                new ExplosionPreset(68, "⚡ Рельсотрон")
            }),
            new PayloadCategory("💣 Авиабомбы", new ExplosionPreset[] {
                new ExplosionPreset(85, "✈️ Стандартная авиабомба"),
                new ExplosionPreset(48, "✈️ Взведенная авиабомба"),
                new ExplosionPreset(45, "💥 Кассетная бомба"),
                new ExplosionPreset(47, "🔥 Зажигательная бомба"),
                new ExplosionPreset(75, "💧 Водяная бомба"),
                new ExplosionPreset(76, "💧 Малая водяная бомба")
            }),
            new PayloadCategory("🚀 Ракеты", new ExplosionPreset[] {
                new ExplosionPreset(5, "🚀 РПГ"),
                new ExplosionPreset(58, "🎯 Ракета авиаудара"),
                new ExplosionPreset(33, "✈️ Авиационная ракета"),
                new ExplosionPreset(49, "🚀 Ракета транспорта"),
                new ExplosionPreset(51, "🎯 Управляемая ракета"),
                new ExplosionPreset(81, "💥 Кассетный ракетомет"),
                new ExplosionPreset(56, "🌊 Торпеда"),
                new ExplosionPreset(57, "🌊 Подводная торпеда"),
                new ExplosionPreset(59, "🌊 Подводная ракета")
            }),
            new PayloadCategory("🎖️ Снаряды", new ExplosionPreset[] {
                new ExplosionPreset(6, "🎖️ Танковый снаряд"),
                new ExplosionPreset(44, "🎖️ Снаряд БТР"),
                new ExplosionPreset(53, "🎖️ Граната танка"),
                new ExplosionPreset(39, "🚁 Пушка Valkyrie")
            }),
            new PayloadCategory("💣 Гранаты", new ExplosionPreset[] {
                new ExplosionPreset(1, "💣 Граната"),
                new ExplosionPreset(2, "💣 Гранатомет"),
                new ExplosionPreset(86, "💥 Кассетный гранатомет"),
                new ExplosionPreset(41, "💣 Трубчатая бомба")
            }),
            new PayloadCategory("🧨 Мины", new ExplosionPreset[] {
                new ExplosionPreset(38, "🧨 Мина"),
                new ExplosionPreset(80, "🧨 Мина-растяжка"),
                new ExplosionPreset(42, "🧨 Мина транспорта"),
                new ExplosionPreset(54, "✈️ Авиационная мина"),
                new ExplosionPreset(61, "⚡ Кинетическая мина"),
                new ExplosionPreset(63, "🔪 Мина-шипы")
            }),
            new PayloadCategory("💥 Липучки", new ExplosionPreset[] {
                new ExplosionPreset(3, "💣 Липкая бомба")
            }),
            new PayloadCategory("🔥 Зажигательные", new ExplosionPreset[] {
                new ExplosionPreset(4, "🔥 Коктейль Молотова"),
                new ExplosionPreset(31, "🔥 Направленное пламя")
            }),
            new PayloadCategory("🚗 Взрывы транспорта", new ExplosionPreset[] {
                new ExplosionPreset(8, "🚗 Легковое авто"),
                new ExplosionPreset(11, "🏍️ Мотоцикл"),
                new ExplosionPreset(16, "🚢 Лодка"),
                new ExplosionPreset(18, "🚚 Грузовик"),
                new ExplosionPreset(9, "✈️ Самолет"),
                new ExplosionPreset(17, "⛴️ Взрыв корабля"),
                new ExplosionPreset(79, "⛴️ Малый взрыв корабля"),
                new ExplosionPreset(30, "🎈 Дирижабль"),
                new ExplosionPreset(27, "🚂 Поезд")
            }),
            new PayloadCategory("⛽ Топливные", new ExplosionPreset[] {
                new ExplosionPreset(32, "🚛 Цистерна"),
                new ExplosionPreset(7, "⛽ Высокооктановое топливо"),
                new ExplosionPreset(10, "⛽ Бензоколонка"),
                new ExplosionPreset(35, "🔥 Газовый бак"),
                new ExplosionPreset(29, "🔥 Пропан")
            }),
            new PayloadCategory("🎯 Взрывные пули", new ExplosionPreset[] {
                new ExplosionPreset(19, "💥 Взрывные пули"),
                new ExplosionPreset(43, "💥 Взрывные патроны"),
                new ExplosionPreset(72, "💥 Взрывная картечь"),
                new ExplosionPreset(34, "💥 Пуля транспорта"),
                new ExplosionPreset(83, "💥 Пуля транспорта 2")
            }),
            new PayloadCategory("📦 Разное", new ExplosionPreset[] {
                new ExplosionPreset(28, "🛢️ Бочка"),
                new ExplosionPreset(24, "🔥 Газовый баллон"),
                new ExplosionPreset(36, "🎆 Фейерверк"),
                new ExplosionPreset(40, "🎯 ПВО"),
                new ExplosionPreset(55, "🌊 Взрыв подлодки"),
                new ExplosionPreset(66, "🚁 Скриптовый дрон"),
                new ExplosionPreset(26, "⚡ Программируемый AR")
            })
        };
    }
}
