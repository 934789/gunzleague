namespace GunZLeague.Models.ViewModels
{
    public class PartnerStreamStatusViewModel
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public bool StatusUnavailable { get; set; }
        public string? Title { get; set; }
        public string? GameName { get; set; }
        public int? ViewerCount { get; set; }
    }
}
