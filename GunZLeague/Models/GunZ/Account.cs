namespace GunZLeague.Models.GunZ
{
    /// <summary>
    /// Represents the GunZ Account table.
    /// </summary>
    public class Account
    {
        public int AID { get; set; }
        public string UserID { get; set; } = string.Empty;
        public int UGradeID { get; set; }
        public int PGradeID { get; set; }
        public DateTime RegDate { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Status { get; set; }
        public DateTime? LastLoginTime { get; set; }
        public DateTime? LastLogoutTime { get; set; }
    }
}
