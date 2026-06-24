using devopsnet.Services;
using Xunit;

namespace devopsnet.Tests.Services;

public class GitCloneServiceTests
{
    [Fact]
    public async Task CloneAsync_ShouldThrow_WhenUrlIsInvalid()
    {
        // Arrange
        var service = new GitCloneService();
        var userId = Guid.NewGuid();

        // Act + Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CloneAsync("https://github.com/ce-repo-nexiste-pas-12345/rien.git", "main", "fake-token", userId));

        Assert.Contains("Échec du clonage", exception.Message);
    }

    [Fact]
    public async Task CloneAsync_ShouldCreateTargetDirectory_BeforeAttemptingClone()
    {
        // Arrange
        var service = new GitCloneService();
        var userId = Guid.NewGuid();
        var basePath = Path.Combine(Path.GetTempPath(), "devopsnet-clones", userId.ToString());

        // Act
        try
        {
            await service.CloneAsync("https://github.com/ce-repo-nexiste-pas-12345/rien.git", "main", "fake-token", userId);
        }
        catch (InvalidOperationException)
        {
            // attendu : le clone échoue, on vérifie juste que le dossier parent a bien été créé avant l'échec
        }

        // Assert
        Assert.True(Directory.Exists(basePath));
    }
}