using GunZLeague.Models.GunZ;

namespace GunZLeague.Models.ViewModels
{
    public class PlayerDetailsViewModel
    {
        public Character Player { get; set; } = new();
        public Account? Account { get; set; }
        public Clan? Clan { get; set; }
        public string ClanGradeName { get; set; } = string.Empty;
        public string RankName { get; set; } = "Normal";
        public DateTime? LastLoginAt { get; set; }
        public int? ContributionPoints { get; set; }
        public DateTime? ClanJoinedAt { get; set; }
    }
}
