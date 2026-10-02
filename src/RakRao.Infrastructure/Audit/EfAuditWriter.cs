using RakRao.Application.Audit;
using RakRao.Domain.Audit;
using RakRao.Infrastructure.Persistence;

namespace RakRao.Infrastructure.Audit;

public sealed class EfAuditWriter(RakRaoDbContext db) : IAuditWriter
{
    public void Add(AuditEvent auditEvent) => db.AuditEvents.Add(auditEvent);
}
