using GunZLeague.Models.GunZ;

namespace GunZLeague.Models.ViewModels
{
    public class ClanDetailsViewModel
    {
        public Clan Clan { get; set; } = new();
        public string MasterName { get; set; } = "Unknown";
        public int MemberCount { get; set; }
        public IReadOnlyList<ClanMemberViewModel> Members { get; set; } = Array.Empty<ClanMemberViewModel>();
    }

    public class ClanMemberViewModel
    {
        public int CID { get; set; }
        public string Name { get; set; } = "Unknown";
        public short? Level { get; set; }
        public int? KillCount { get; set; }
        public int? DeathCount { get; set; }
        public string GradeName { get; set; } = "Member";
        public int? ContributionPoints { get; set; }
        public DateTime? JoinedAt { get; set; }
        public bool IsMaster { get; set; }
    }
}
