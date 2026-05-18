using Microsoft.EntityFrameworkCore;
using GunZLeague.Models.GunZ;

namespace GunZLeague.Data
{
    public class GunZDbContext : DbContext
    {
        public GunZDbContext(DbContextOptions<GunZDbContext> options) : base(options)
        {
        }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<LoginAccount> Logins { get; set; }
        public DbSet<UserGrade> UserGrades { get; set; }
        public DbSet<PremiumGrade> PremiumGrades { get; set; }
        public DbSet<Character> Characters { get; set; }
        public DbSet<Clan> Clans { get; set; }
        public DbSet<ClanMember> ClanMembers { get; set; }
        public DbSet<ClanMemberGrade> ClanMemberGrades { get; set; }
        public DbSet<PWGameLog> PlayerWarLogs { get; set; }
        public DbSet<PWCharacterInfo> PlayerWarCharacters { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable("Account");
                entity.HasKey(e => e.AID);
                entity.Property(e => e.UserID).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(50);
                entity.Property(e => e.Status).HasMaxLength(1).IsFixedLength();
            });

            modelBuilder.Entity<LoginAccount>(entity =>
            {
                entity.ToTable("Login");
                entity.HasKey(e => e.UserID);
                entity.Property(e => e.UserID).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Password).HasMaxLength(20);
                entity.Property(e => e.LastIP).HasMaxLength(20);
            });

            modelBuilder.Entity<UserGrade>(entity =>
            {
                entity.ToTable("UserGrade");
                entity.HasKey(e => e.UGradeID);
                entity.Property(e => e.Name).HasMaxLength(20);
            });

            modelBuilder.Entity<PremiumGrade>(entity =>
            {
                entity.ToTable("PremiumGrade");
                entity.HasKey(e => e.PGradeID);
                entity.Property(e => e.Name).HasMaxLength(20);
            });

            modelBuilder.Entity<Character>(entity =>
            {
                entity.ToTable("Character");
                entity.HasKey(e => e.CID);
                entity.Property(e => e.Name).HasMaxLength(24).IsRequired();
            });

            modelBuilder.Entity<Clan>(entity =>
            {
                entity.ToTable("Clan");
                entity.HasKey(e => e.CLID);
                entity.Property(e => e.Name).HasMaxLength(24);
                entity.Property(e => e.Introduction).HasMaxLength(1024);
                entity.Property(e => e.Homepage).HasMaxLength(128);
                entity.Property(e => e.EmblemUrl).HasMaxLength(256);
                entity.Property(e => e.EmblemChecksum);
                entity.Property(e => e.WinCount).HasColumnName("Wins");
                entity.Property(e => e.LoseCount).HasColumnName("Losses");
                entity.Property(e => e.XP).HasColumnName("Exp");
            });

            modelBuilder.Entity<ClanMember>(entity =>
            {
                entity.ToTable("ClanMember");
                entity.HasKey(e => e.CMID);
            });

            modelBuilder.Entity<ClanMemberGrade>(entity =>
            {
                entity.ToTable("ClanMemberGrade");
                entity.HasKey(e => e.GradeID);
                entity.Property(e => e.Grade).HasMaxLength(24);
            });

            modelBuilder.Entity<PWGameLog>(entity =>
            {
                entity.ToTable("PWGameLog");
                entity.HasNoKey();
                entity.Property(e => e.Winners).HasMaxLength(256);
                entity.Property(e => e.Losers).HasMaxLength(256);
            });

            modelBuilder.Entity<PWCharacterInfo>(entity =>
            {
                entity.ToTable("PWCharacterInfo");
                entity.HasKey(e => e.CID);
                entity.Property(e => e.Name).HasMaxLength(100);
            });
        }
    }
}
