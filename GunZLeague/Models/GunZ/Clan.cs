namespace GunZLeague.Models.GunZ
{
    /// <summary>
    /// Represents the GunZ Clan table.
    /// </summary>
    public class Clan
    {
        public int CLID { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? MasterCID { get; set; }
        public byte? Level { get; set; }
        public int? XP { get; set; }
        public int? Point { get; set; }
        public int? TotalPoint { get; set; }
        public int? WinCount { get; set; }
        public int? LoseCount { get; set; }
        public int? Draws { get; set; }
        public int? Ranking { get; set; }
        public DateTime? RegDate { get; set; }
        public string? Introduction { get; set; }
        public string? Homepage { get; set; }
        public string? EmblemUrl { get; set; }
        public int? EmblemChecksum { get; set; }
        public byte? DeleteFlag { get; set; }
    }
}
