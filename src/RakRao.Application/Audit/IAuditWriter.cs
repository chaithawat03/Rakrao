using RakRao.Domain.Audit;

namespace RakRao.Application.Audit;

// Add to the caller's scoped unit of work; the caller commits its business row and audit row together.
public interface IAuditWriter
{
    void Add(AuditEvent auditEvent);
}
