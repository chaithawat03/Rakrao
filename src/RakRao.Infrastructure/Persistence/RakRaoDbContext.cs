using Microsoft.EntityFrameworkCore;
using RakRao.Domain.Audit;
using RakRao.Domain.Families;
using RakRao.Domain.Identity;

namespace RakRao.Infrastructure.Persistence;

public sealed class RakRaoDbContext(DbContextOptions<RakRaoDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Family> Families => Set<Family>();
    public DbSet<FamilyMembership> FamilyMemberships => Set<FamilyMembership>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

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
            entity.HasOne<Family>().WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.RequestId).HasColumnName("request_id").IsRequired();
            entity.Property(x => x.Reason).HasColumnName("reason");
            entity.Property(x => x.SafeDiff).HasColumnName("safe_diff").HasColumnType("jsonb");
            entity.HasIndex(x => new { x.TargetType, x.TargetId, x.OccurredAt })
                .IsDescending(false, false, true);
            entity.HasIndex(x => new { x.FamilyId, x.OccurredAt })
                .IsDescending(false, true);
        });

        modelBuilder.Entity<Family>(entity =>
        {
            entity.ToTable("families", table => table.HasCheckConstraint("ck_families_status", "status IN ('ACTIVE','ARCHIVED')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FamilyMembership>(entity =>
        {
            entity.ToTable("family_memberships", table => table.HasCheckConstraint("ck_family_memberships_status", "status IN ('ACTIVE','SUSPENDED','LEFT')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.FamilyId).HasColumnName("family_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.JoinedAt).HasColumnName("joined_at");
            entity.Property(x => x.LeftAt).HasColumnName("left_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
            entity.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
            entity.HasIndex(x => new { x.FamilyId, x.UserId }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Status });
            entity.HasIndex(x => new { x.FamilyId, x.Status });
            entity.HasOne<Family>().WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoleAssignment>(entity =>
        {
            entity.ToTable("role_assignments", table => table.HasCheckConstraint("ck_role_assignments_scope",
                "(role = 'SUPER_ADMIN' AND family_id IS NULL) OR (role IN ('FAMILY_MEMBER','FAMILY_ADMIN','CREATOR') AND family_id IS NOT NULL)"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.FamilyId).HasColumnName("family_id");
            entity.Property(x => x.Role).HasColumnName("role").IsRequired();
            entity.Property(x => x.GrantedByUserId).HasColumnName("granted_by_user_id");
            entity.Property(x => x.GrantedAt).HasColumnName("granted_at");
            entity.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            entity.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.HasIndex(x => new { x.FamilyId, x.UserId, x.Role }).IsUnique().HasFilter("revoked_at IS NULL AND family_id IS NOT NULL");
            entity.HasIndex(x => new { x.UserId, x.Role }).IsUnique().HasFilter("revoked_at IS NULL AND family_id IS NULL");
            entity.HasIndex(x => new { x.FamilyId, x.Role }).HasFilter("revoked_at IS NULL");
            entity.HasOne<Family>().WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
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
