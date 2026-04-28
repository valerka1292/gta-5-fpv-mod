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

        public static ExplosionPreset ActivePayload = new ExplosionPreset(69, "Script Drone");

        public static int CurrentExplosionId => ActivePayload.Id;
        public static string CurrentExplosionName => ActivePayload.Name;

        public static readonly PayloadCategory[] Categories = new PayloadCategory[]
        {
            new PayloadCategory("Tactical / Precision", new ExplosionPreset[] {
                new ExplosionPreset(69, "Script Drone (Kamikaze)"),
                new ExplosionPreset(72, "Script Missile"),
                new ExplosionPreset(2, "Sticky Bomb"),
                new ExplosionPreset(40, "Proximity Mine"),
                new ExplosionPreset(43, "Pipe Bomb")
            }),
            new PayloadCategory("Heavy / Artillery", new ExplosionPreset[] {
                new ExplosionPreset(5, "Tank Shell"),
                new ExplosionPreset(4, "RPG Rocket"),
                new ExplosionPreset(59, "Orbital Cannon"),
                new ExplosionPreset(36, "Railgun"),
                new ExplosionPreset(46, "APC Shell")
            }),
            new PayloadCategory("Aviation Bombs", new ExplosionPreset[] {
                new ExplosionPreset(50, "Standard Bomb"),
                new ExplosionPreset(60, "Wide Standard Bomb"),
                new ExplosionPreset(47, "Cluster Bomb"),
                new ExplosionPreset(49, "Incendiary Bomb"),
                new ExplosionPreset(48, "Gas Bomb")
            }),
            new PayloadCategory("Vehicle Weapons", new ExplosionPreset[] {
                new ExplosionPreset(41, "Valkyrie Cannon"),
                new ExplosionPreset(56, "Hunter Cannon"),
                new ExplosionPreset(57, "Rogue Cannon"),
                new ExplosionPreset(62, "Oppressor MK2 Cannon"),
                new ExplosionPreset(73, "RC Tank Rocket")
            }),
            new PayloadCategory("Non-Lethal / Special", new ExplosionPreset[] {
                new ExplosionPreset(78, "Flash Grenade"),
                new ExplosionPreset(79, "Stun Grenade"),
                new ExplosionPreset(20, "Smoke Grenade"),
                new ExplosionPreset(83, "EMP Launcher"),
                new ExplosionPreset(38, "Firework")
            })
        };
    }
}
