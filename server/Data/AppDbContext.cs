using LanChat.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanChat.Server.Data
{
    public sealed class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<ChatGroup> ChatGroups => Set<ChatGroup>();
        public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<FileTransfer> FileTransfers => Set<FileTransfer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Username).IsRequired().HasMaxLength(50);
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.HasIndex(u => u.Username).IsUnique();
            });

            modelBuilder.Entity<ChatGroup>(entity =>
            {
                entity.HasKey(g => g.Id);
                entity.Property(g => g.GroupName).IsRequired().HasMaxLength(100);
                entity.Property(g => g.CreatorId).IsRequired();

                entity.HasOne(g => g.Creator)
                    .WithMany(u => u.CreatedGroups)
                    .HasForeignKey(g => g.CreatorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(g => g.Members)
                    .WithOne(gm => gm.Group)
                    .HasForeignKey(gm => gm.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(g => g.Messages)
                    .WithOne(m => m.Group)
                    .HasForeignKey(m => m.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<GroupMember>(entity =>
            {
                entity.HasKey(gm => new { gm.GroupId, gm.UserId });

                entity.HasOne(gm => gm.Group)
                    .WithMany(g => g.Members)
                    .HasForeignKey(gm => gm.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(gm => gm.User)
                    .WithMany(u => u.GroupMemberships)
                    .HasForeignKey(gm => gm.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Content).IsRequired();
                entity.Property(m => m.SentAt).IsRequired();

                entity.HasOne(m => m.Sender)
                    .WithMany(u => u.SentMessages)
                    .HasForeignKey(m => m.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Receiver)
                    .WithMany(u => u.ReceivedMessages)
                    .HasForeignKey(m => m.ReceiverId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Group)
                    .WithMany(g => g.Messages)
                    .HasForeignKey(m => m.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_Message_ReceiverOrGroup",
                    "((ReceiverId IS NULL) <> (GroupId IS NULL))"));
            });

            modelBuilder.Entity<FileTransfer>(entity =>
            {
                entity.HasKey(ft => ft.Id);
                entity.Property(ft => ft.FileName).IsRequired();
                entity.Property(ft => ft.Status).IsRequired().HasMaxLength(20);
                entity.Property(ft => ft.RequestedAt).IsRequired();

                entity.HasOne(ft => ft.Sender)
                    .WithMany(u => u.SentFileTransfers)
                    .HasForeignKey(ft => ft.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ft => ft.Receiver)
                    .WithMany(u => u.ReceivedFileTransfers)
                    .HasForeignKey(ft => ft.ReceiverId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_FileTransfer_Status",
                    "Status IN ('Pending', 'Completed', 'Rejected', 'Failed')"));
            });
        }
    }
}
