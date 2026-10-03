namespace RakRao.Application.Families;

public sealed record CreateFamilyRequest(string? Name, string? Description);
public enum FamilyCreateOutcome { Created, NotProvisioned, Throttled, Conflict }
public sealed record FamilyCreateResult(FamilyCreateOutcome Outcome, FamilySummary? Family);
public sealed record FamilySummary(Guid Id, string Name, string? Description, int Version,
    IReadOnlyList<string> Roles, IReadOnlyList<string> Capabilities);
public sealed record ChangeFamilyRoleRequest(string? Role, bool? Grant);
public enum RoleChangeOutcome { Changed, NotFound, Forbidden, Conflict }
public sealed record UpdateFamilyRequest(string? Name, string? Description);
public enum FamilyUpdateOutcome { Updated, NotFound, Forbidden, Conflict }
public sealed record FamilyUpdateResult(FamilyUpdateOutcome Outcome, FamilySummary? Family);

public interface IFamilyService
{
    Task<FamilyCreateResult> CreateAsync(string firebaseUid, CreateFamilyRequest request,
        string? idempotencyKey, string requestId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<FamilySummary>> ListAsync(string firebaseUid, CancellationToken cancellationToken);
    Task<FamilySummary?> GetAsync(string firebaseUid, Guid familyId, CancellationToken cancellationToken);
    Task<RoleChangeOutcome> ChangeRoleAsync(string firebaseUid, Guid familyId, Guid memberId,
        ChangeFamilyRoleRequest request, string requestId, CancellationToken cancellationToken);
    Task<FamilyUpdateResult> UpdateAsync(string firebaseUid, Guid familyId, int expectedVersion,
        UpdateFamilyRequest request,
        string requestId, CancellationToken cancellationToken);
}
