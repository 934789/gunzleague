namespace GunZLeague.Models.GunZ
{
    public class PlayerWarStanding
    {
        public int? CharacterId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public int Points { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
    }
}
