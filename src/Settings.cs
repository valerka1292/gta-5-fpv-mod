namespace FpvDroneMod
{
    // Runtime-mutable configuration that lives outside Config.cs (Config is
    // const). Set from Menu.cs; consumed by Collision.Detonate and friends.
    //
    // The explosion-type IDs come from GTA V's native ExplosionType enum
    // (see ADD_EXPLOSION) — the user's reference list:
    //
    //    0  Grenade           — classic burst, grey smoke
    //    1  Grenade Launcher  — slightly tighter flash
    //    2  Sticky Bomb       — clean compact burst
    //    4  Rocket / RPG      — large fireball, dense black smoke
    //    5  Tank Shell        — sharp, almost smokeless flash, very lethal
    //    7  Car               — fuel-tank-style long burn
    //    8  Plane             — huge radius, lots of debris
    //    9  Petrol Pump       — fire mushroom
    //   29  Blimp             — biggest in-game explosion
    internal static class Settings
    {
        public struct ExplosionPreset
        {
            public int Id;
            public string Name;
            public ExplosionPreset(int id, string name) { Id = id; Name = name; }
        }

        public static readonly ExplosionPreset[] ExplosionPresets = new ExplosionPreset[]
        {
            new ExplosionPreset(0,  "Grenade"),
            new ExplosionPreset(1,  "Grenade Launcher"),
            new ExplosionPreset(2,  "Sticky Bomb"),
            new ExplosionPreset(4,  "Rocket / RPG"),
            new ExplosionPreset(5,  "Tank Shell"),
            new ExplosionPreset(7,  "Car"),
            new ExplosionPreset(8,  "Plane"),
            new ExplosionPreset(9,  "Petrol Pump"),
            new ExplosionPreset(29, "Blimp"),
        };

        // Default index → 0 = Grenade (matches the spec's "Type 2 = Grenade"
        // legacy comment, even though the actual ID for Grenade is 0).
        public static int ExplosionPresetIndex = 0;

        public static int CurrentExplosionId => ExplosionPresets[ExplosionPresetIndex].Id;
        public static string CurrentExplosionName => ExplosionPresets[ExplosionPresetIndex].Name;
    }
}
