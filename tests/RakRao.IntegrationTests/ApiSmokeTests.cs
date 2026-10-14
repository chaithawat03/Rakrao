using Microsoft.AspNetCore.Mvc.Testing;

namespace RakRao.IntegrationTests;

public class ApiSmokeTests
{
    [Fact]
    public async Task Health_returns_success_and_request_id()
    {
        using var factory = FirestoreFixture.Factory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
        Assert.True(response.Headers.Contains("X-Request-ID"));
    }
}
