using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RakRao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreatorSafetyGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE families ADD CONSTRAINT ck_families_nonblank_name
                    CHECK (length(btrim(name)) > 0);
                CREATE INDEX ix_families_normalized_name ON families (lower(btrim(name)));
                CREATE INDEX ix_audit_family_creation_retry
                    ON audit_events (actor_user_id, (safe_diff->>'idempotencyKeyHash'))
                    WHERE action = 'FAMILY_CREATED' AND safe_diff IS NOT NULL;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM families f
                        WHERE f.status = 'ACTIVE' AND NOT EXISTS (
                            SELECT 1 FROM family_memberships m
                            JOIN role_assignments r ON r.family_id = m.family_id AND r.user_id = m.user_id
                            WHERE m.family_id = f.id AND m.status = 'ACTIVE'
                              AND r.role = 'CREATOR' AND r.revoked_at IS NULL
                        )
                    ) THEN
                        RAISE EXCEPTION 'existing active family lacks an active Creator' USING ERRCODE = '23514';
                    END IF;
                END $$;

                CREATE FUNCTION rakrao_assert_active_creator(target_family_id uuid) RETURNS void
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF target_family_id IS NULL THEN RETURN; END IF;
                    PERFORM 1 FROM families WHERE id = target_family_id AND status = 'ACTIVE' FOR UPDATE;
                    IF FOUND AND NOT EXISTS (
                        SELECT 1 FROM family_memberships m
                        JOIN role_assignments r ON r.family_id = m.family_id AND r.user_id = m.user_id
                        WHERE m.family_id = target_family_id AND m.status = 'ACTIVE'
                          AND r.role = 'CREATOR' AND r.revoked_at IS NULL
                    ) THEN
                        RAISE EXCEPTION 'active family requires an active Creator' USING ERRCODE = '23514';
                    END IF;
                END $$;

                CREATE FUNCTION rakrao_check_active_creator() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE target_family_id uuid;
                BEGIN
                    IF TG_TABLE_NAME = 'families' THEN
                        IF TG_OP = 'DELETE' THEN target_family_id := OLD.id;
                        ELSE target_family_id := NEW.id; END IF;
                    ELSE
                        IF TG_OP = 'DELETE' THEN target_family_id := OLD.family_id;
                        ELSE target_family_id := NEW.family_id; END IF;
                    END IF;
                    PERFORM rakrao_assert_active_creator(target_family_id);
                    IF TG_OP = 'UPDATE' AND TG_TABLE_NAME <> 'families' THEN
                        IF OLD.family_id IS DISTINCT FROM NEW.family_id THEN
                            PERFORM rakrao_assert_active_creator(OLD.family_id);
                        END IF;
                    END IF;
                    RETURN NULL;
                END $$;

                CREATE CONSTRAINT TRIGGER trg_families_active_creator
                    AFTER INSERT OR UPDATE ON families
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
                    EXECUTE FUNCTION rakrao_check_active_creator();
                CREATE CONSTRAINT TRIGGER trg_memberships_active_creator
                    AFTER INSERT OR UPDATE OR DELETE ON family_memberships
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
                    EXECUTE FUNCTION rakrao_check_active_creator();
                CREATE CONSTRAINT TRIGGER trg_roles_active_creator
                    AFTER INSERT OR UPDATE OR DELETE ON role_assignments
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
                    EXECUTE FUNCTION rakrao_check_active_creator();

                CREATE FUNCTION rakrao_reject_creator_truncate() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'cannot truncate family membership or role grants'
                        USING ERRCODE = '2F000';
                END $$;
                CREATE TRIGGER trg_memberships_no_truncate BEFORE TRUNCATE ON family_memberships
                    FOR EACH STATEMENT EXECUTE FUNCTION rakrao_reject_creator_truncate();
                CREATE TRIGGER trg_roles_no_truncate BEFORE TRUNCATE ON role_assignments
                    FOR EACH STATEMENT EXECUTE FUNCTION rakrao_reject_creator_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_families_normalized_name;
                DROP INDEX IF EXISTS ix_audit_family_creation_retry;
                ALTER TABLE families DROP CONSTRAINT IF EXISTS ck_families_nonblank_name;
                DROP TRIGGER IF EXISTS trg_roles_no_truncate ON role_assignments;
                DROP TRIGGER IF EXISTS trg_memberships_no_truncate ON family_memberships;
                DROP FUNCTION IF EXISTS rakrao_reject_creator_truncate();
                DROP TRIGGER trg_roles_active_creator ON role_assignments;
                DROP TRIGGER trg_memberships_active_creator ON family_memberships;
                DROP TRIGGER trg_families_active_creator ON families;
                DROP FUNCTION rakrao_check_active_creator();
                DROP FUNCTION rakrao_assert_active_creator(uuid);
                """);
        }
    }
}
