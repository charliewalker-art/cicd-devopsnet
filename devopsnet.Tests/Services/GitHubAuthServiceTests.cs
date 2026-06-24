using System.Net;
using System.Text;
using devopsnet.Data;
using devopsnet.Options;
using devopsnet.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace devopsnet.Tests.Services;

public class GitHubAuthServiceTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static IDataProtectionProvider CreateDataProtectionProvider()
    {
        return DataProtectionProvider.Create("devopsnet-tests");
    }

    private static HttpClient CreateSequencedMockedHttpClient(string tokenResponseJson, string profileResponseJson)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        var callCount = 0;

        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var json = callCount == 1 ? tokenResponseJson : profileResponseJson;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            });

        return new HttpClient(mockHandler.Object);
    }

    private static GitHubAuthService CreateService(AppDbContext context, HttpClient httpClient)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new GitHubOptions
        {
            ClientId = "fake-client-id",
            ClientSecret = "fake-client-secret",
            CallbackUrl = "https://localhost:7198/api/auth/github/callback",
        });

        return new GitHubAuthService(context, httpClient, options, CreateDataProtectionProvider());
    }

    [Fact]
    public async Task LinkAccountAsync_ShouldCreateGitHubAccount_WhenNoneExists()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();

        var tokenResponse = """{ "access_token": "gho_fake123" }""";
        var profileResponse = """{ "id": 999, "login": "charlie-gh" }""";
        var httpClient = CreateSequencedMockedHttpClient(tokenResponse, profileResponse);

        var service = CreateService(context, httpClient);

        // Act
        await service.LinkAccountAsync(userId, "fake-code");

        // Assert
        var account = await context.GitHubAccounts.FirstAsync();
        Assert.Equal(userId, account.UserId);
        Assert.Equal(999, account.GitHubId);
        Assert.Equal("charlie-gh", account.GitHubUsername);
        Assert.NotEqual("gho_fake123", account.EncryptedAccessToken);
    }

    [Fact]
    public async Task LinkAccountAsync_ShouldUpdateExistingAccount_WhenAlreadyLinked()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();

        var firstHttpClient = CreateSequencedMockedHttpClient(
            """{ "access_token": "gho_old" }""",
            """{ "id": 999, "login": "charlie-gh" }""");
        var firstService = CreateService(context, firstHttpClient);
        await firstService.LinkAccountAsync(userId, "code-1");

        var secondHttpClient = CreateSequencedMockedHttpClient(
            """{ "access_token": "gho_new" }""",
            """{ "id": 999, "login": "charlie-gh-renamed" }""");
        var secondService = CreateService(context, secondHttpClient);

        // Act
        await secondService.LinkAccountAsync(userId, "code-2");

        // Assert
        var accounts = await context.GitHubAccounts.ToListAsync();
        Assert.Single(accounts);
        Assert.Equal("charlie-gh-renamed", accounts[0].GitHubUsername);
    }

    [Fact]
    public async Task GetDecryptedTokenAsync_ShouldReturnOriginalToken_AfterEncryption()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();

        var tokenResponse = """{ "access_token": "gho_roundtrip" }""";
        var profileResponse = """{ "id": 1, "login": "test-user" }""";
        var httpClient = CreateSequencedMockedHttpClient(tokenResponse, profileResponse);

        var service = CreateService(context, httpClient);
        await service.LinkAccountAsync(userId, "fake-code");

        // Act
        var decryptedToken = await service.GetDecryptedTokenAsync(userId);

        // Assert
        Assert.Equal("gho_roundtrip", decryptedToken);
    }

    [Fact]
    public async Task GetDecryptedTokenAsync_ShouldThrow_WhenNoAccountLinked()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var httpClient = new HttpClient();
        var service = CreateService(context, httpClient);

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetDecryptedTokenAsync(Guid.NewGuid()));
    }
}