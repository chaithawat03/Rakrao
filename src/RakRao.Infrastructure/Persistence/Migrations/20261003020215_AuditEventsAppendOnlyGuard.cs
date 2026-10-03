using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RakRao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditEventsAppendOnlyGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION public.reject_audit_event_changes()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'Audit events are append-only.' USING ERRCODE = 'P0001';
                    RETURN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER audit_events_reject_update_delete
                BEFORE UPDATE OR DELETE ON public.audit_events
                FOR EACH STATEMENT
                EXECUTE FUNCTION public.reject_audit_event_changes();

                CREATE TRIGGER audit_events_reject_truncate
                BEFORE TRUNCATE ON public.audit_events
                FOR EACH STATEMENT
                EXECUTE FUNCTION public.reject_audit_event_changes();

                ALTER TABLE public.audit_events ENABLE ALWAYS TRIGGER audit_events_reject_update_delete;
                ALTER TABLE public.audit_events ENABLE ALWAYS TRIGGER audit_events_reject_truncate;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER audit_events_reject_truncate ON public.audit_events;
                DROP TRIGGER audit_events_reject_update_delete ON public.audit_events;
                DROP FUNCTION public.reject_audit_event_changes();
                """);
        }
    }
}
