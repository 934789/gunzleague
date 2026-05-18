namespace GunZLeague.Models.GunZ
{
    public class PlayerWarMatchSummary
    {
        public string Winners { get; set; } = string.Empty;
        public string Losers { get; set; } = string.Empty;
        public IReadOnlyList<string> WinnerPlayers { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> LoserPlayers { get; set; } = Array.Empty<string>();
        public int? WinScore { get; set; }
        public int? LoseScore { get; set; }
        public string RoundScore { get; set; } = string.Empty;
        public int? MapID { get; set; }
        public string MapName { get; set; } = string.Empty;
        public DateTime? PlayedAt { get; set; }
    }
}
