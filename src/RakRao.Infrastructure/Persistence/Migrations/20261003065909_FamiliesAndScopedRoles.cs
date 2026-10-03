using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RakRao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FamiliesAndScopedRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "families",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_families", x => x.id);
                    table.CheckConstraint("ck_families_status", "status IN ('ACTIVE','ARCHIVED')");
                    table.ForeignKey(
                        name: "FK_families_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_families_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "family_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_family_memberships", x => x.id);
                    table.CheckConstraint("ck_family_memberships_status", "status IN ('ACTIVE','SUSPENDED','LEFT')");
                    table.ForeignKey(
                        name: "FK_family_memberships_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_family_memberships_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_family_memberships_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_family_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role = table.Column<string>(type: "text", nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_assignments", x => x.id);
                    table.CheckConstraint("ck_role_assignments_scope", "(role = 'SUPER_ADMIN' AND family_id IS NULL) OR (role IN ('FAMILY_MEMBER','FAMILY_ADMIN','CREATOR') AND family_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_role_assignments_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_assignments_users_granted_by_user_id",
                        column: x => x.granted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_families_created_by_user_id",
                table: "families",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_families_updated_by_user_id",
                table: "families",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_family_memberships_created_by_user_id",
                table: "family_memberships",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_family_memberships_family_id_status",
                table: "family_memberships",
                columns: new[] { "family_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_family_memberships_family_id_user_id",
                table: "family_memberships",
                columns: new[] { "family_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_family_memberships_updated_by_user_id",
                table: "family_memberships",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_family_memberships_user_id_status",
                table: "family_memberships",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_family_id_role",
                table: "role_assignments",
                columns: new[] { "family_id", "role" },
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_family_id_user_id_role",
                table: "role_assignments",
                columns: new[] { "family_id", "user_id", "role" },
                unique: true,
                filter: "revoked_at IS NULL AND family_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_granted_by_user_id",
                table: "role_assignments",
                column: "granted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_user_id_role",
                table: "role_assignments",
                columns: new[] { "user_id", "role" },
                unique: true,
                filter: "revoked_at IS NULL AND family_id IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_audit_events_families_family_id",
                table: "audit_events",
                column: "family_id",
                principalTable: "families",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_events_families_family_id",
                table: "audit_events");

            migrationBuilder.DropTable(
                name: "family_memberships");

            migrationBuilder.DropTable(
                name: "role_assignments");

            migrationBuilder.DropTable(
                name: "families");
        }
    }
}
