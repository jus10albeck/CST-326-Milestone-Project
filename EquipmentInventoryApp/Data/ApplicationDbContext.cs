
using EquipmentInventoryApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Sets up the database tables
        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Equipment> Equipment { get; set; }
        public DbSet<Checkout> Checkouts { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<DamageReport> DamageReports { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<EquipmentHistory> EquipmentHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Makes sure emails, categories, and serial numbers are unique
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.CategoryName)
                .IsUnique();

            modelBuilder.Entity<Equipment>()
                .HasIndex(e => e.SerialNumber)
                .IsUnique();

            // Speeds up loading one item's history
            modelBuilder.Entity<EquipmentHistory>()
                .HasIndex(h => h.EquipmentId);

            // Connects equipment to its category
            modelBuilder.Entity<Equipment>()
                .HasOne(e => e.Category)
                .WithMany(c => c.Equipment)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Connects checkout records to users and equipment
            modelBuilder.Entity<Checkout>()
                .HasOne(c => c.User)
                .WithMany(u => u.Checkouts)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Checkout>()
                .HasOne(c => c.Equipment)
                .WithMany(e => e.Checkouts)
                .HasForeignKey(c => c.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Connects reservations to users and equipment
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reservations)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Equipment)
                .WithMany(e => e.Reservations)
                .HasForeignKey(r => r.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Connects damage reports to users and equipment
            modelBuilder.Entity<DamageReport>()
                .HasOne(d => d.User)
                .WithMany(u => u.DamageReports)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DamageReport>()
                .HasOne(d => d.Equipment)
                .WithMany(e => e.DamageReports)
                .HasForeignKey(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Connects notifications to the user receiving them
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
