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
                new ExplosionPreset(69, "SCRIPT DRONE (KAMIKAZE)"),
                new ExplosionPreset(72, "SCRIPT MISSILE")
            }),
            new PayloadCategory("MINES / IED", new ExplosionPreset[] {
                new ExplosionPreset(40, "PROXIMITY MINE"),
                new ExplosionPreset(44, "VEHICLE MINE"),
                new ExplosionPreset(71, "BURIED MINE"),
                new ExplosionPreset(64, "KINETIC MINE")
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
