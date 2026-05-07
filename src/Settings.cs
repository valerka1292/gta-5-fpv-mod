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

        // --- Глобальные настройки ---
        public static bool GiveStars = true;
        public static int DamageOverrideIndex = 7; // По умолчанию 1000 HP
        public static readonly float[] DamageValues = { 0, 50, 100, 200, 400, 600, 800, 1000, 1500, 2000, 3000, 5000 };

        // --- Настройка силы физического импульса ---
        public struct ImpulsePreset
        {
            public string Label;
            public float ForceScale;     // Множитель базовой силы
            public float Radius;         // Радиус в метрах
            public float CameraShake;    // Интенсивность тряски (0.0 - 10.0+)
            public bool Invisible;       // Скрыть стандартный визуальный эффект (для малых пресетов)
            public bool Audible;         // Слышимость взрыва
        }

        public static int ImpulseScaleIndex = 2; // По умолчанию MEDIUM (NORMAL)
        public static readonly ImpulsePreset[] ImpulsePresets = {
            new ImpulsePreset { Label = "OFF",          ForceScale = 0f,     Radius = 0f,   CameraShake = 0f,    Invisible = true,  Audible = false },
            new ImpulsePreset { Label = "WEAK",         ForceScale = 0.2f,   Radius = 3f,   CameraShake = 0.2f,  Invisible = true,  Audible = true  },
            new ImpulsePreset { Label = "LIGHT",        ForceScale = 0.5f,   Radius = 5f,   CameraShake = 0.5f,  Invisible = false, Audible = true  },
            new ImpulsePreset { Label = "MEDIUM",       ForceScale = 1.0f,   Radius = 8f,   CameraShake = 1.0f,  Invisible = false, Audible = true  },
            new ImpulsePreset { Label = "HEAVY",        ForceScale = 2.5f,   Radius = 12f,  CameraShake = 1.5f,  Invisible = false, Audible = true  },
            new ImpulsePreset { Label = "STRONG",       ForceScale = 5.0f,   Radius = 20f,  CameraShake = 2.0f,  Invisible = false, Audible = true  },
            new ImpulsePreset { Label = "TACTICAL",     ForceScale = 12.0f,  Radius = 35f,  CameraShake = 3.0f,  Invisible = false, Audible = true  },
            new ImpulsePreset { Label = "BATTLEFIELD",  ForceScale = 25.0f,  Radius = 60f,  CameraShake = 4.5f,  Invisible = false, Audible = true  },
            new ImpulsePreset { Label = "DEVASTATING",  ForceScale = 50.0f,  Radius = 100f, CameraShake = 6.0f,  Invisible = false, Audible = true  },
            new ImpulsePreset { Label = "EXTREME",      ForceScale = 100.0f, Radius = 150f, CameraShake = 8.0f,  Invisible = false, Audible = true  },
            new ImpulsePreset { Label = "CATASTROPHIC", ForceScale = 250.0f, Radius = 250f, CameraShake = 15.0f, Invisible = false, Audible = true  },
            new ImpulsePreset { Label = "APOCALYPTIC",  ForceScale = 600.0f, Radius = 450f, CameraShake = 30.0f, Invisible = false, Audible = true  },
            new ImpulsePreset { Label = "NUCLEAR",      ForceScale = 1500.0f, Radius = 800f, CameraShake = 100.0f, Invisible = false, Audible = true  }
        };

        public static ImpulsePreset CurrentImpulsePreset
        {
            get
            {
                int idx = ImpulseScaleIndex;
                if (idx < 0) idx = 0;
                if (idx >= ImpulsePresets.Length) idx = ImpulsePresets.Length - 1;
                return ImpulsePresets[idx];
            }
        }

        public static ExplosionPreset ActivePayload = new ExplosionPreset(0, "GRENADE");

        public static int CurrentExplosionId => ActivePayload.Id;
        public static string CurrentExplosionName => ActivePayload.Name;

        public static readonly PayloadCategory[] Categories = new PayloadCategory[]
        {
            new PayloadCategory("ANTIPERSONNEL / LIGHT", new ExplosionPreset[] {
                new ExplosionPreset(0, "GRENADE"),
                new ExplosionPreset(1, "GRENADE LAUNCHER"),
                new ExplosionPreset(2, "STICKY BOMB"),
                new ExplosionPreset(43, "PIPEBOMB"),
                new ExplosionPreset(3, "MOLOTOV"),
                new ExplosionPreset(83, "EMP LAUNCHER")
            }),
            new PayloadCategory("ANTI-TANK / HEAVY", new ExplosionPreset[] {
                new ExplosionPreset(4, "RPG ROCKET"),
                new ExplosionPreset(5, "TANK SHELL"),
                new ExplosionPreset(46, "APC SHELL"),
                new ExplosionPreset(36, "RAILGUN"),
                new ExplosionPreset(69, "SCRIPT_DRONE (KAMIKAZE)"),
                new ExplosionPreset(72, "SCRIPT_MISSILE")
            }),
            new PayloadCategory("MINES / IED", new ExplosionPreset[] {
                new ExplosionPreset(40, "PROXIMITY MINE"),
                new ExplosionPreset(44, "VEHICLE MINE"),
                new ExplosionPreset(71, "BURIED MINE")
            }),
            new PayloadCategory("AVIATION PAYLOAD", new ExplosionPreset[] {
                new ExplosionPreset(50, "BOMB STANDARD"),
                new ExplosionPreset(60, "BOMB WIDE"),
                new ExplosionPreset(47, "BOMB CLUSTER"),
                new ExplosionPreset(49, "BOMB INCENDIARY"),
                new ExplosionPreset(51, "TORPEDO")
            }),
            new PayloadCategory("FUEL / GAS", new ExplosionPreset[] {
                new ExplosionPreset(6, "HI-OCTANE FUEL"),
                new ExplosionPreset(9, "PETROL PUMP"),
                new ExplosionPreset(28, "PROPANE TANK"),
                new ExplosionPreset(27, "EXPLOSIVE BARREL"),
                new ExplosionPreset(34, "GAS TANK")
            })
        };
    }
}
