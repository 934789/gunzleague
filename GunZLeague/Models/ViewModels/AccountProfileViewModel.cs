using GunZLeague.Models.GunZ;

namespace GunZLeague.Models.ViewModels
{
    public class AccountProfileViewModel
    {
        public IReadOnlyList<Character> Characters { get; set; } = Array.Empty<Character>();
        public IReadOnlyList<AccountClanPanelViewModel> Clans { get; set; } = Array.Empty<AccountClanPanelViewModel>();
        public int? SelectedClanId { get; set; }
        public AccountClanPanelViewModel? SelectedClan { get; set; }
    }

    public class AccountClanPanelViewModel
    {
        public int CLID { get; set; }
        public string ClanName { get; set; } = string.Empty;
        public string? EmblemUrl { get; set; }
        public int? EmblemChecksum { get; set; }
        public string ActorCharacterName { get; set; } = string.Empty;
        public string ActorGradeName { get; set; } = "Member";
        public bool CanManageMembers { get; set; }
        public bool CanUpdateEmblem { get; set; }
        public IReadOnlyList<AccountClanMemberViewModel> Members { get; set; } = Array.Empty<AccountClanMemberViewModel>();
    }

    public class AccountClanMemberViewModel
    {
        public int CMID { get; set; }
        public int CID { get; set; }
        public string Name { get; set; } = "Unknown";
        public string GradeName { get; set; } = "Member";
        public short? Level { get; set; }
        public int? ContributionPoints { get; set; }
        public bool CanKick { get; set; }
    }
}
