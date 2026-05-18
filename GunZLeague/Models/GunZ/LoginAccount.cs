namespace GunZLeague.Models.GunZ
{
    /// <summary>
    /// Represents the GunZ Login table.
    /// </summary>
    public class LoginAccount
    {
        public string UserID { get; set; } = string.Empty;
        public int AID { get; set; }
        public string? Password { get; set; }
        public DateTime? LastConnDate { get; set; }
        public string? LastIP { get; set; }
    }
}
