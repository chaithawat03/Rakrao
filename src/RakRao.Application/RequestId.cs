namespace RakRao.Application;

public static class RequestId
{
    public static string Resolve(string? candidate)
    {
        if (!string.IsNullOrEmpty(candidate) && candidate.Length <= 64 &&
            candidate.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
        {
            return candidate;
        }

        return Guid.NewGuid().ToString("N");
    }
}
