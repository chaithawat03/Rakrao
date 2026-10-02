using Microsoft.EntityFrameworkCore;
using RakRao.Domain.Audit;
using RakRao.Domain.Identity;

namespace RakRao.Infrastructure.Persistence;

public sealed class RakRaoDbContext(DbContextOptions<RakRaoDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.FirebaseUid).HasColumnName("firebase_uid").IsRequired();
            entity.HasIndex(x => x.FirebaseUid).IsUnique();
            entity.Property(x => x.DisplayName).HasColumnName("display_name");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone");
            entity.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Action).HasColumnName("action").IsRequired();
            entity.Property(x => x.TargetType).HasColumnName("target_type").IsRequired();
            entity.Property(x => x.TargetId).HasColumnName("target_id");
            entity.Property(x => x.FamilyId).HasColumnName("family_id");
            entity.Property(x => x.RequestId).HasColumnName("request_id").IsRequired();
            entity.Property(x => x.Reason).HasColumnName("reason");
            entity.Property(x => x.SafeDiff).HasColumnName("safe_diff").HasColumnType("jsonb");
            entity.HasIndex(x => new { x.TargetType, x.TargetId, x.OccurredAt })
                .IsDescending(false, false, true);
            entity.HasIndex(x => new { x.FamilyId, x.OccurredAt })
                .IsDescending(false, true);
        });
    }

    private void EnsureAuditAppendOnly()
    {
        if (ChangeTracker.Entries<AuditEvent>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit events are append-only.");
    }

    public override int SaveChanges()
    {
        EnsureAuditAppendOnly();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAuditAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuditAppendOnly();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureAuditAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
