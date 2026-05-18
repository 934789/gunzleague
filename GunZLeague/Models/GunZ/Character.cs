namespace GunZLeague.Models.GunZ
{
    /// <summary>
    /// Represents the GunZ Character table.
    /// </summary>
    public class Character
    {
        public int CID { get; set; }
        public int AID { get; set; }
        public string Name { get; set; } = string.Empty;
        public short? Level { get; set; }
        public byte? Sex { get; set; }
        public int? KillCount { get; set; }
        public int? DeathCount { get; set; }
        public int? XP { get; set; }
        public int? BP { get; set; }
        public DateTime? RegDate { get; set; }
        public DateTime? LastTime { get; set; }
        public int? PlayTime { get; set; }
        public int? GameCount { get; set; }
        public byte? DeleteFlag { get; set; }
    }
}
