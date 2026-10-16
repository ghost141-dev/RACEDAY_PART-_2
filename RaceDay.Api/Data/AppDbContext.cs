using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core context. Table names, column types, keys, unique indexes, check constraints and
/// defaults are configured to match the Part 1 ERD and RaceDay_Database.sql script.
/// All foreign keys use Restrict (the SQL script uses the default NO ACTION).
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<EventRoute> Routes => Set<EventRoute>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<RaceResult> Results => Set<RaceResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser', 'Participant')"));
            e.HasKey(x => x.UserId);
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UQ_Users_Email");
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20);
            e.Property(x => x.ProfilePictureUrl).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Events", t => t.HasCheckConstraint("CK_Events_Type", "[EventType] IN ('Run', 'Walk', 'Cycle')"));
            e.HasKey(x => x.EventId);
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.EventDate).HasColumnType("datetime");
            e.Property(x => x.Location).HasMaxLength(200).IsRequired();
            e.Property(x => x.DistanceKm).HasColumnType("decimal(6,2)");
            e.Property(x => x.EventType).HasMaxLength(20).IsRequired();
            e.Property(x => x.BannerImageUrl).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Organiser)
                .WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserId)
                .HasConstraintName("FK_Events_Organiser")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.DistanceKm).HasColumnType("decimal(6,2)");
            e.HasOne(x => x.Event)
                .WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventId)
                .HasConstraintName("FK_Categories_Events")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EventRoute>(e =>
        {
            e.ToTable("Routes");
            e.HasKey(x => x.RouteId);
            e.Property(x => x.StartPoint).HasMaxLength(200).IsRequired();
            e.Property(x => x.EndPoint).HasMaxLength(200).IsRequired();
            e.Property(x => x.ElevationGainM).HasColumnType("decimal(6,2)");
            e.Property(x => x.RouteMapUrl).HasMaxLength(500);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.Event)
                .WithMany(ev => ev.Routes)
                .HasForeignKey(x => x.EventId)
                .HasConstraintName("FK_Routes_Events")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t => t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Pending', 'Confirmed')"));
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue(EnrolmentStatuses.Pending);
            e.Property(x => x.EnrolledAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => new { x.ParticipantId, x.EventId }).IsUnique().HasDatabaseName("UQ_Enrolments_ParticipantEvent");
            e.HasOne(x => x.Participant)
                .WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantId)
                .HasConstraintName("FK_Enrolments_Participant")
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Event)
                .WithMany(ev => ev.Enrolments)
                .HasForeignKey(x => x.EventId)
                .HasConstraintName("FK_Enrolments_Events")
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category)
                .WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId)
                .HasConstraintName("FK_Enrolments_Categories")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RaceResult>(e =>
        {
            e.ToTable("Results");
            e.HasKey(x => x.ResultId);
            e.Property(x => x.FinishTime).HasColumnType("time");
            e.Property(x => x.CapturedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.EnrolmentId).IsUnique().HasDatabaseName("UQ_Results_Enrolment");
            e.HasOne(x => x.Enrolment)
                .WithOne(en => en.Result)
                .HasForeignKey<RaceResult>(x => x.EnrolmentId)
                .HasConstraintName("FK_Results_Enrolments")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
