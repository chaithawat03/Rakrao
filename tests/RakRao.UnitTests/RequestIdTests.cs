using RakRao.Application;

namespace RakRao.UnitTests;

public class RequestIdTests
{
    [Fact]
    public void Resolve_preserves_safe_client_id()
    {
        Assert.Equal("client-123", RequestId.Resolve("client-123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("line\nbreak")]
    public void Resolve_replaces_unsafe_client_id(string input)
    {
        var result = RequestId.Resolve(input);
        Assert.True(Guid.TryParse(result, out _));
    }
}
