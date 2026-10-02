namespace RakRao.Application.Identity;

public sealed record VerifiedFirebaseUser(string Uid, string? DisplayName);

public interface IFirebaseIdTokenVerifier
{
    Task<VerifiedFirebaseUser?> VerifyAsync(string token, CancellationToken cancellationToken);
}
