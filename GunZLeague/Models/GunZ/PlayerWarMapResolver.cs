namespace GunZLeague.Models.GunZ
{
    public static class PlayerWarMapResolver
    {
        private static readonly IReadOnlyDictionary<int, string> MapNames = new Dictionary<int, string>
        {
            [0] = "Mansion",
            [1] = "Prison",
            [2] = "Station",
            [3] = "Prison II",
            [4] = "Battle Arena",
            [5] = "Town",
            [6] = "Dungeon",
            [7] = "Ruin",
            [8] = "Island",
            [9] = "Garden",
            [10] = "Castle",
            [11] = "Factory",
            [12] = "Port",
            [13] = "Lost Shrine",
            [14] = "Stairway",
            [15] = "Snowtown",
            [16] = "Hall",
            [17] = "Catacomb",
            [18] = "Jail",
            [19] = "Shower Room",
            [20] = "High Haven",
            [21] = "Citadel",
            [22] = "Relay Map",
            [23] = "Halloween Town",
            [24] = "Weapon Shop",
            [25] = "Blitzkrieg",
            [26] = "Test A",
            [27] = "Test B",
            [28] = "Skirmish Hall",
            [29] = "Town New",
            [30] = "Mansion New",
            [31] = "Station New",
            [32] = "Garden New",
            [33] = "Factory New",
            [34] = "Port New"
        };

        public static string GetName(int? mapId)
        {
            if (!mapId.HasValue)
            {
                return "-";
            }

            return MapNames.TryGetValue(mapId.Value, out var mapName)
                ? mapName
                : $"Map {mapId.Value}";
        }
    }
}
