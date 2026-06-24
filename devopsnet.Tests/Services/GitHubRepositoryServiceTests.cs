using System.Net;
using System.Text;
using devopsnet.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace devopsnet.Tests.Services;

public class GitHubRepositoryServiceTests
{
    private static HttpClient CreateMockedHttpClient(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });

        return new HttpClient(mockHandler.Object);
    }

    [Fact]
    public async Task GetUserRepositoriesAsync_ShouldMapFields_WhenResponseIsValid()
    {
        // Arrange
        var fakeJson = """
        [
          {
            "name": "devopsnet",
            "full_name": "charlie/devopsnet",
            "clone_url": "https://github.com/charlie/devopsnet.git",
            "private": false,
            "default_branch": "main"
          },
          {
            "name": "projet-prive",
            "full_name": "charlie/projet-prive",
            "clone_url": "https://github.com/charlie/projet-prive.git",
            "private": true,
            "default_branch": "develop"
          }
        ]
        """;

        var httpClient = CreateMockedHttpClient(fakeJson);
        var service = new GitHubRepositoryService(httpClient);

        // Act
        var result = await service.GetUserRepositoriesAsync("fake-token");

        // Assert
        Assert.Equal(2, result.Count);

        Assert.Equal("devopsnet", result[0].Name);
        Assert.Equal("charlie/devopsnet", result[0].FullName);
        Assert.False(result[0].IsPrivate);
        Assert.Equal("main", result[0].DefaultBranch);

        Assert.Equal("projet-prive", result[1].Name);
        Assert.True(result[1].IsPrivate);
        Assert.Equal("develop", result[1].DefaultBranch);
    }

    [Fact]
    public async Task GetUserRepositoriesAsync_ShouldReturnEmptyList_WhenNoRepositories()
    {
        // Arrange
        var httpClient = CreateMockedHttpClient("[]");
        var service = new GitHubRepositoryService(httpClient);

        // Act
        var result = await service.GetUserRepositoriesAsync("fake-token");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUserRepositoriesAsync_ShouldThrow_WhenGitHubReturnsError()
    {
        // Arrange
        var httpClient = CreateMockedHttpClient("{\"message\": \"Bad credentials\"}", HttpStatusCode.Unauthorized);
        var service = new GitHubRepositoryService(httpClient);

        // Act + Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.GetUserRepositoriesAsync("token-invalide"));
    }
}