namespace GunZLeague.Models.GunZ
{
    /// <summary>
    /// Represents Player War match logs.
    /// </summary>
    public class PWGameLog
    {
        public string Winners { get; set; } = string.Empty;
        public string Losers { get; set; } = string.Empty;
        public int? WinScore { get; set; }
        public int? LoseScore { get; set; }
        public int? MapID { get; set; }
        public DateTime? RegDate { get; set; }
    }
}
