namespace GunZLeague.Models.GunZ
{
    public class ClanMember
    {
        public int CMID { get; set; }
        public int CLID { get; set; }
        public int CID { get; set; }
        public byte? Grade { get; set; }
        public DateTime? RegDate { get; set; }
        public int? ContPoint { get; set; }
    }
}
