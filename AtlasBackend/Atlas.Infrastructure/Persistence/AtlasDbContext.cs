using Atlas.Domain.Audit;
using Atlas.Domain.Model.Applications;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Infrastructure.Persistence;

public class AtlasDbContext : DbContext
{
    public DbSet<OnboardingApplication> Applications => Set<OnboardingApplication>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    public AtlasDbContext(DbContextOptions<AtlasDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Application aggregate
        modelBuilder.Entity<OnboardingApplication>(builder =>
        {
            builder.ToTable("Applications");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.Market)
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(a => a.AccountNumber).HasMaxLength(64);
            builder.Property(a => a.CardReference).HasMaxLength(64);
            builder.Property(a => a.RejectionReason).HasMaxLength(500);
            builder.Property(a => a.ComplianceReviewedBy).HasMaxLength(100);
            builder.Property(a => a.ComplianceReviewNotes).HasMaxLength(1000);

            // Owned Applicant
            builder.OwnsOne(a => a.Applicant, app =>
            {
                app.Property(p => p.FirstName).HasMaxLength(100);
                app.Property(p => p.LastName).HasMaxLength(100);
                app.Property(p => p.NationalId).HasMaxLength(50);
                app.Property(p => p.PassportNumber).HasMaxLength(50);
                app.Property(p => p.Email).HasMaxLength(150);
                app.Property(p => p.Phone).HasMaxLength(50);
                app.Property(p => p.DateOfBirth).HasConversion(
                    d => d.ToDateTime(TimeOnly.MinValue),
                    d => DateOnly.FromDateTime(d));
            });

            // Owned IdentityVerification
            builder.OwnsOne(a => a.IdentityVerification, iv =>
            {
                iv.Property(p => p.IdentificationId).HasMaxLength(100);
                iv.Property(p => p.ResultStatus).HasConversion<string>().HasMaxLength(50);
            });

            // Owned SanctionsScreening
            builder.OwnsOne(a => a.SanctionsScreening, ss =>
            {
                ss.Property(p => p.CaseId).HasMaxLength(100);
                ss.Property(p => p.Status).HasConversion<string>().HasMaxLength(50);
                ss.Property(p => p.MatchDetails).HasMaxLength(1000);
            });

            // Owned Documents
            builder.OwnsMany(a => a.Documents, doc =>
            {
                doc.ToTable("ApplicationDocuments");
                doc.WithOwner().HasForeignKey("ApplicationId");
                doc.HasKey("Id");
                doc.Property(d => d.Type).HasConversion<string>().HasMaxLength(50);
                doc.Property(d => d.ContentHash).HasMaxLength(128);
                doc.Property(d => d.StorageReference).HasMaxLength(256);
            });

            builder.Navigation(a => a.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // Audit Log Entry
        modelBuilder.Entity<AuditLogEntry>(builder =>
        {
            builder.ToTable("AuditLogs");
            builder.HasKey(l => l.Id);
            builder.Property(l => l.PerformedBy).HasMaxLength(100).IsRequired();
            builder.Property(l => l.Action).HasConversion<string>().HasMaxLength(50).IsRequired();
            builder.Property(l => l.Details).HasMaxLength(2000).IsRequired();

            builder.HasIndex(l => l.ApplicationId);
            builder.HasIndex(l => l.CreatedAtUtc);
        });

    }
}
