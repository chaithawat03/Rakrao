namespace RakRao.Application.Identity;

public sealed record MeResponse(Guid Id, string? DisplayName, string OnboardingState,
    IReadOnlyList<object> Families);

public interface IMeService
{
    Task<MeResponse?> GetAsync(string firebaseUid, CancellationToken cancellationToken);
    Task<MeResponse> ProvisionAsync(VerifiedFirebaseUser firebaseUser, string requestId,
        CancellationToken cancellationToken);
}
